using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService
{
    private const string Criacao = "criar";

    public async Task<RespostaComVersao<AluguelDetalhe>> CriarAsync(ComandoAluguel comando, string? chave, CancellationToken ct)
    {
        if (chave is null || chave.Length is 0 or > 128 || chave.Any(c => c < '!' || c > '~' || c == ','))
            throw new ValidacaoException("idempotencyKey", "Informe uma chave de idempotência válida.");
        var ator = escrita.UsuarioId ?? throw new UnauthorizedAccessException("Ator não autenticado.");
        var hash = HashComando(comando);

        return await repositorio.ExecutarEscritaAsync(async token =>
        {
            var registro = await repositorio.LerChaveAsync(ator, Criacao, chave, token);
            if (registro is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(registro.HashComando, hash))
                    throw new ConflitoException("A chave de idempotência já foi usada com outros dados.", "idempotencyKey");
                var (original, snapshot) = await repositorio.LerResultadoOriginalAsync(registro.RevisaoId, token);
                return new RespostaComVersao<AluguelDetalhe>(
                    Detalhe(original, snapshot) with { Estado = EstadoAluguel.EmAberto }, Etag.De(registro.VersaoOriginal));
            }

            var agora = relogio.Agora.ToUniversalTime();
            var dados = await PrepararAsync(comando, agora, null, null, token);
            var aluguel = new Aluguel { EmpresaId = escrita.EmpresaId, CriadoEm = agora, SaidaUtc = agora };
            var revisao = Capturar(aluguel, null, dados, comando.HodometroInicial!.Value, agora);
            aluguel.Aplicar(revisao, agora);
            repositorio.Adicionar(aluguel);
            repositorio.Adicionar(revisao);
            escrita.Auditar("aluguel.criado", nameof(Aluguel), aluguel.Id, CamposContrato);
            var novoRegistro = new RegistroIdempotenciaAluguel
            {
                EmpresaId = aluguel.EmpresaId, UsuarioId = ator, Operacao = Criacao, Chave = chave, HashComando = hash,
                RevisaoId = revisao.Id, CriadoEm = agora, AtualizadoEm = agora,
            };
            await repositorio.RegistrarResultadoAsync(aluguel, novoRegistro, token);
            return new RespostaComVersao<AluguelDetalhe>(Detalhe(aluguel, revisao), Etag.De(aluguel.Versao));
        }, criacaoIdempotente: true, ct);
    }

    public async Task<RespostaComVersao<AluguelDetalhe>> EditarAsync(Guid id, ComandoAluguel comando,
        string? ifMatch, CancellationToken ct)
    {
        var (aluguel, revisao) = await repositorio.ExecutarEscritaAsync(async token =>
        {
            var (atual, anterior) = await repositorio.LerAtualAsync(id, true, token) ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, atual.Versao);
            if (atual.Estado != EstadoAluguel.EmAberto)
                throw new ConflitoException("Somente aluguéis em aberto podem ser editados.");
            var dados = await PrepararAsync(comando, atual.SaidaUtc, atual.Id, anterior, token);
            var agora = relogio.Agora.ToUniversalTime();
            var nova = Capturar(atual, anterior, dados, comando.HodometroInicial!.Value, agora);
            atual.Aplicar(nova, agora);
            repositorio.Adicionar(nova);
            escrita.Auditar("aluguel.editado", nameof(Aluguel), atual.Id, CamposContrato);
            return (atual, nova);
        }, criacaoIdempotente: false, ct);
        return new(Detalhe(aluguel, revisao), Etag.De(aluguel.Versao));
    }

    private static readonly string[] CamposContrato =
    [
        "clienteId", "condutorId", "veiculoId", "planoId", "retornoPrevisto", "hodometroInicial", "taxaIds", "seguros",
    ];

    private static byte[] HashComando(ComandoAluguel comando) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        comando.ClienteId, comando.CondutorId, comando.VeiculoId, comando.PlanoId,
        Retorno = RetornoCanonico(comando.RetornoPrevisto),
        comando.HodometroInicial, Taxas = (comando.TaxaIds ?? []).Order().ToArray(),
        Seguros = (comando.Seguros ?? []).Order().ToArray(),
    }));

    private static string RetornoCanonico(string? retorno)
    {
        // Uma chave já utilizada conflita mesmo se a nova intenção contiver data inválida.
        // Na primeira execução, PrepararAsync ainda valida a entrada antes de gravar.
        try { return "utc:" + HorarioLocacao.InterpretarRetorno(retorno).UtcTicks.ToString(CultureInfo.InvariantCulture); }
        catch (ValidacaoException) { return "entrada:" + retorno; }
    }

    private static ContratoSnapshot Capturar(Aluguel aluguel, ContratoSnapshot? anterior, DadosPreparados dados,
        int hodometro, DateTimeOffset agora)
    {
        var (cliente, condutor, veiculo, plano, _, _) = dados.Referencias;
        var estimativa = dados.Estimativa;
        var mesmoCliente = anterior?.ClienteId == cliente.Id;
        var mesmoCondutor = anterior?.CondutorId == condutor.Id;
        var mesmoVeiculo = anterior?.VeiculoId == veiculo.Id;
        var mesmoGrupo = anterior?.GrupoId == veiculo.GrupoId;
        var revisao = new ContratoSnapshot
        {
            EmpresaId = aluguel.EmpresaId, AluguelId = aluguel.Id, Numero = checked(aluguel.RevisaoAtual + 1),
            CriadoEm = agora, AtualizadoEm = agora,
            ClienteId = cliente.Id, ClienteNome = mesmoCliente ? anterior!.ClienteNome : cliente.Nome,
            ClienteTipo = mesmoCliente ? anterior!.ClienteTipo : cliente.Tipo,
            ClienteDocumento = mesmoCliente ? anterior!.ClienteDocumento : (cliente.Cpf ?? cliente.Cnpj)!,
            CondutorId = condutor.Id, CondutorNome = mesmoCondutor ? anterior!.CondutorNome : condutor.Nome,
            CondutorCpf = mesmoCondutor ? anterior!.CondutorCpf : condutor.Cpf,
            CondutorCnh = mesmoCondutor ? anterior!.CondutorCnh : condutor.Cnh,
            ValidadeCnh = mesmoCondutor ? anterior!.ValidadeCnh : condutor.ValidadeCnh,
            VeiculoId = veiculo.Id, VeiculoPlaca = mesmoVeiculo ? anterior!.VeiculoPlaca : veiculo.Placa,
            VeiculoMarca = mesmoVeiculo ? anterior!.VeiculoMarca : veiculo.Marca,
            VeiculoModelo = mesmoVeiculo ? anterior!.VeiculoModelo : veiculo.Modelo,
            Combustivel = mesmoVeiculo ? anterior!.Combustivel : veiculo.Combustivel,
            CapacidadeLitros = mesmoVeiculo ? anterior!.CapacidadeLitros : veiculo.CapacidadeLitros,
            GrupoId = veiculo.GrupoId, GrupoNome = mesmoGrupo ? anterior!.GrupoNome : veiculo.Grupo!.Nome,
            PlanoId = plano.Id, ModalidadePlano = estimativa.Parametros.Modalidade,
            PrecoDiario = estimativa.Parametros.PrecoDiario, PrecoKm = estimativa.Parametros.PrecoKm,
            FranquiaDiariaKm = estimativa.Parametros.FranquiaDiariaKm,
            RetornoPrevistoUtc = dados.Retorno.ToUniversalTime(), HodometroInicial = hodometro,
            Diarias = estimativa.Diarias, ValorPlano = estimativa.Plano.Valor, FranquiaTotalKm = estimativa.Plano.FranquiaTotalKm,
            ParcelasConhecidas = estimativa.ParcelasConhecidas, Caucao = estimativa.Caucao,
        };
        revisao.Itens = estimativa.Adicionais.Select(x => new ItemContratado
        {
            EmpresaId = aluguel.EmpresaId, RevisaoId = revisao.Id, CriadoEm = agora, AtualizadoEm = agora,
            TaxaId = x.Item.Categoria == CategoriaAdicional.Taxa ? x.Item.OrigemId : null,
            SeguroId = x.Item.Categoria == CategoriaAdicional.Taxa ? null : x.Item.OrigemId,
            Categoria = x.Item.Categoria, Nome = x.Item.Nome, Modalidade = x.Item.Modalidade, Preco = x.Item.Preco,
            Valor = x.Valor,
        }).ToList();
        return revisao;
    }

    private static AluguelDetalhe Detalhe(Aluguel aluguel, ContratoSnapshot revisao)
    {
        var (cliente, condutor, veiculo, grupo) = (
            new ContratanteLocacao(revisao.ClienteId, revisao.ClienteNome, revisao.ClienteTipo,
                revisao.ClienteTipo == TipoCliente.PF ? "CPF •••.•••.•••-••" : "CNPJ ••.•••.•••/••••-••"),
            new CondutorLocacao(revisao.CondutorId, revisao.CondutorNome, revisao.ValidadeCnh),
            new VeiculoLocacao(revisao.VeiculoId, revisao.VeiculoPlaca, revisao.VeiculoMarca, revisao.VeiculoModelo,
                revisao.Combustivel, Decimais.FormatacaoCapacidade(revisao.CapacidadeLitros)),
            new GrupoLocacao(revisao.GrupoId, revisao.GrupoNome));
        var estimativa = new DemonstrativoEstimado(revisao.Diarias, PlanoDto(revisao.PlanoId, revisao.ModalidadePlano,
                revisao.PrecoDiario, revisao.PrecoKm, revisao.FranquiaDiariaKm),
            Valor(revisao.ValorPlano), revisao.FranquiaTotalKm, revisao.ModalidadePlano != ModalidadePlano.Livre,
            revisao.Itens.OrderBy(x => x.Categoria).ThenBy(x => x.TaxaId).Select(x => new ComponenteContratado(
                x.TaxaId ?? x.SeguroId!.Value, x.Categoria, x.Nome, x.Modalidade, Decimais.FormatacaoMonetaria(x.Preco),
                Valor(x.Valor))).ToArray(),
            Valor(revisao.ParcelasConhecidas), Valor(revisao.Caucao), true);
        return new(aluguel.Id, aluguel.Estado, cliente, condutor, veiculo, grupo, HorarioLocacao.ParaSaoPaulo(aluguel.SaidaUtc),
            HorarioLocacao.ParaSaoPaulo(revisao.RetornoPrevistoUtc), revisao.HodometroInicial, estimativa);
    }
}
