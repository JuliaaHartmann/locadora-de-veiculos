using System.Security.Cryptography;
using Locadora.Domain;

namespace Locadora.Application;

public sealed class FotosService(IRepositorioFotos repositorio, IProtocoloEscrita escrita, IRelogio relogio,
    ISaneadorFoto saneador, IArmazenamentoFotos armazenamento, LimiteProcessamentoFotos limite)
{
    public async Task<ConteudoFoto> ObterAsync(Guid veiculoId, CancellationToken ct)
    {
        var objeto = await repositorio.LerAtivaAsync(veiculoId, false, ct) ?? throw new NaoEncontradoException();
        if (objeto.Deposito != armazenamento.Deposito) throw FalhaFotoException.Indisponivel();
        using var prazo = Prazo(ct, LimitesFoto.Armazenamento);
        Stream? fluxo = null;
        try
        {
            fluxo = await armazenamento.LerAsync(objeto, prazo.Token) ?? throw FalhaFotoException.Indisponivel();
            // O objeto não fica acessível se uma alteração venceu enquanto o armazenamento abria o fluxo.
            var atual = await repositorio.LerAtivaAsync(veiculoId, false, prazo.Token);
            if (atual?.Id != objeto.Id) throw new NaoEncontradoException();
            return new(fluxo, objeto.Mime, objeto.Tamanho);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (fluxo is not null) await fluxo.DisposeAsync();
            throw FalhaFotoException.Indisponivel();
        }
        catch
        {
            if (fluxo is not null) await fluxo.DisposeAsync();
            throw;
        }
    }

    public async Task<string> EnviarAsync(Guid veiculoId, string? ifMatch,
        Func<CancellationToken, Task<EntradaFoto>> lerEntrada, CancellationToken ct)
    {
        await repositorio.ValidarVeiculoAsync(veiculoId, ifMatch, false, ct);
        if (!await limite.Vaga.WaitAsync(0, ct)) throw FalhaFotoException.Ocupado();
        try
        {
            return await EnviarComVagaAsync(veiculoId, ifMatch, lerEntrada, ct);
        }
        finally { limite.Vaga.Release(); }
    }

    private async Task<string> EnviarComVagaAsync(Guid veiculoId, string? ifMatch,
        Func<CancellationToken, Task<EntradaFoto>> lerEntrada, CancellationToken ct)
    {
        ImagemSaneada imagem;
        using (var processamento = Prazo(ct, LimitesFoto.Processamento))
        {
            try
            {
                imagem = saneador.Sanear(await lerEntrada(processamento.Token), processamento.Token);
                processamento.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw FalhaFotoException.Ocupado(); }
        }

        await RetomarAsync(ct);
        var objeto = await escrita.ExecutarAsync(async token =>
        {
            await repositorio.ValidarVeiculoAsync(veiculoId, ifMatch, false, token);
            var agora = relogio.Agora;
            var resultado = new FotoObjeto
            {
                EmpresaId = escrita.EmpresaId, VeiculoId = veiculoId,
                UrlRelativa = $"/api/v1/veiculos/{veiculoId:D}/foto",
                Deposito = armazenamento.Deposito, Chave = $"{escrita.EmpresaId:N}/{Guid.NewGuid():N}",
                Mime = imagem.Mime, Tamanho = imagem.Bytes.Count, Hash = Convert.ToHexString(SHA256.HashData(imagem.Bytes.AsSpan())),
                Estado = EstadoFoto.Pendente, RetomarAposUtc = agora + LimitesFoto.Reserva, CriadoEm = agora, AtualizadoEm = agora,
            };
            repositorio.Adicionar(resultado);
            return resultado;
        }, ct);
        repositorio.LimparRastreamento();

        try
        {
            using var prazo = Prazo(ct, LimitesFoto.Armazenamento);
            for (var tentativa = 0; ; tentativa++)
            {
                try
                {
                    await armazenamento.GravarAsync(objeto, imagem.Bytes, prazo.Token);
                    await using var gravado = await armazenamento.LerAsync(objeto, prazo.Token);
                    if (gravado is null || !string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(gravado, prazo.Token)),
                        objeto.Hash, StringComparison.Ordinal))
                        throw FalhaFotoException.Indisponivel();
                    break;
                }
                catch (FalhaFotoException) when (tentativa == 0 && !prazo.IsCancellationRequested)
                {
                    // Mesma intenção e chave, dentro do mesmo prazo; não criar nova pendência.
                }
            }
        }
        catch (Exception erro) when (!ct.IsCancellationRequested && erro is FalhaFotoException or OperationCanceledException)
        {
            // A pendência durável já existe; não afirmar nada sobre o objeto externo.
            throw FalhaFotoException.NaoGravada();
        }

        // Um erro de SQL/commit aqui é incerto até nova leitura. Nunca apagar o objeto neste catch.
        var veiculo = await escrita.ExecutarAsync(async token =>
        {
            var v = await repositorio.ValidarVeiculoAsync(veiculoId, ifMatch, true, token);
            var pendente = await repositorio.LerAsync(objeto.Id, true, token);
            var agora = relogio.Agora;
            if (pendente is null || pendente.Estado != EstadoFoto.Pendente || pendente.RetomarAposUtc <= agora)
                throw FalhaFotoException.NaoGravada();
            var anterior = await repositorio.LerAtivaAsync(veiculoId, true, token);
            if (anterior is not null)
            {
                Reter(anterior, agora);
                // Atualiza o índice filtrado antes da ativação, ainda na mesma transação.
                await repositorio.SalvarAsync(token);
            }
            pendente.Estado = EstadoFoto.Ativa;
            pendente.AtualizadoEm = agora;
            repositorio.AtualizarVersao(v, agora);
            escrita.Auditar(anterior is null ? "foto.adicionada" : "foto.substituida", nameof(Veiculo), v.Id, "foto");
            return v;
        }, ct);
        return Etag.De(veiculo.Versao);
    }

    public async Task<string> RemoverAsync(Guid veiculoId, string? ifMatch, CancellationToken ct)
    {
        await repositorio.ValidarVeiculoAsync(veiculoId, ifMatch, false, ct);
        await RetomarAsync(ct);
        var veiculo = await escrita.ExecutarAsync(async token =>
        {
            var v = await repositorio.ValidarVeiculoAsync(veiculoId, ifMatch, true, token);
            var ativa = await repositorio.LerAtivaAsync(veiculoId, true, token) ?? throw new NaoEncontradoException();
            var agora = relogio.Agora;
            Reter(ativa, agora);
            repositorio.AtualizarVersao(v, agora);
            escrita.Auditar("foto.removida", nameof(Veiculo), v.Id, "foto");
            return v;
        }, ct);
        return Etag.De(veiculo.Versao);
    }

    // Uma única pendência da empresa por operação; sem varredura global ou tarefa permanente.
    public async Task RetomarAsync(CancellationToken ct)
    {
        var objeto = await escrita.ExecutarAsync(async token =>
        {
            var agora = relogio.Agora;
            var resultado = await repositorio.SelecionarCompensacaoAsync(armazenamento.Deposito, agora, token);
            if (resultado is null) return null;
            resultado.Estado = EstadoFoto.Orfa; // Não pode mais ser ativado.
            resultado.RetomarAposUtc = agora + LimitesFoto.Reserva;
            resultado.AtualizadoEm = agora;
            return resultado;
        }, ct);
        if (objeto is null) return;
        repositorio.LimparRastreamento();
        var removido = false;
        try
        {
            using var prazo = Prazo(ct, LimitesFoto.Armazenamento);
            await armazenamento.RemoverAsync(objeto, prazo.Token);
            removido = true;
        }
        catch (Exception erro) when (!ct.IsCancellationRequested && erro is FalhaFotoException or OperationCanceledException) { }
        await escrita.ExecutarAsync(async token =>
        {
            var atual = await repositorio.LerAsync(objeto.Id, true, token);
            if (atual?.Estado != EstadoFoto.Orfa || !atual.Versao.SequenceEqual(objeto.Versao)) return false;
            atual.TentativasCompensacao++;
            atual.AtualizadoEm = relogio.Agora;
            if (removido) atual.Estado = EstadoFoto.Removida;
            else if (atual.TentativasCompensacao >= 3) atual.Estado = EstadoFoto.FalhaCompensacao;
            return true;
        }, ct);
        repositorio.LimparRastreamento();
    }

    private static void Reter(FotoObjeto foto, DateTimeOffset agora)
    {
        foto.Estado = EstadoFoto.Retida;
        foto.AtualizadoEm = agora;
    }

    private static CancellationTokenSource Prazo(CancellationToken ct, TimeSpan duracao)
    {
        var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        prazo.CancelAfter(duracao);
        return prazo;
    }
}
