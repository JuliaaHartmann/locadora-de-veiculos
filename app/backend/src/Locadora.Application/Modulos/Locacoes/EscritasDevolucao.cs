using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService
{
    private const string Devolucao = "devolver";

    public async Task OcultarAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        await repositorio.ExecutarEscritaAsync(async token =>
        {
            var (aluguel, _) = await repositorio.LerAtualAsync(id, true, token) ?? throw new NaoEncontradoException();
            Etag.Exigir(ifMatch, aluguel.Versao);
            if (aluguel.Estado != EstadoAluguel.Concluido)
                throw new ConflitoException("Somente aluguéis concluídos podem ser ocultados.", "estado");
            aluguel.ExcluidoEmUtc = relogio.Agora.ToUniversalTime();
            aluguel.AtualizadoEm = aluguel.ExcluidoEmUtc.Value;
            escrita.Auditar("aluguel.ocultado", nameof(Aluguel), aluguel.Id, "oculto");
            return true;
        }, idempotente: false, ct);
    }

    public Task<RespostaComVersao<DevolucaoConcluida>> ConcluirAsync(Guid id, ComandoDevolucao comando,
        string? versaoAceita, string? ifMatch, string? chave, CancellationToken ct)
    {
        chave = ValidarChave(chave);
        var ator = escrita.UsuarioId ?? throw new UnauthorizedAccessException("Ator não autenticado.");
        var hash = HashDevolucao(id, comando, versaoAceita);

        return repositorio.ExecutarEscritaAsync(async token =>
        {
            var registro = await repositorio.LerChaveAsync(ator, Devolucao, chave, token);
            if (registro is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(registro.HashComando, hash))
                    throw new ConflitoException("A chave de idempotência já foi usada com outros dados.", "idempotencyKey");
                var (original, revisaoOriginal) = await repositorio.LerFinalOriginalAsync(registro.FinalId!.Value, token);
                return Resultado(original, revisaoOriginal, registro.VersaoOriginal);
            }

            var (aluguel, revisao) = await repositorio.LerAtualAsync(id, true, token) ?? throw new NaoEncontradoException();
            if (string.IsNullOrWhiteSpace(ifMatch)) throw new PrecondicaoObrigatoriaException();
            ExigirAbertoParaDevolucao(aluguel);
            Etag.Exigir(ifMatch, aluguel.Versao);
            if (versaoAceita is null || versaoAceita.Length != 64)
                throw new ValidacaoException("versaoAceita", "Informe a versão da prévia revisada.");
            var entrada = InterpretarDevolucao(comando);
            var preparada = await PrepararDevolucaoAsync(aluguel, revisao, entrada, token, versaoAceita);

            var agora = relogio.Agora.ToUniversalTime();
            var final = CapturarFinal(aluguel, revisao, entrada, preparada, agora);
            aluguel.Estado = EstadoAluguel.Concluido;
            aluguel.AtualizadoEm = agora;
            repositorio.Adicionar(final);
            escrita.Auditar("aluguel.devolvido", nameof(Aluguel), aluguel.Id,
                "estado", "retornoReal", "hodometroFinal", "litrosRestantes", "taxaIds", "final");
            var novoRegistro = new RegistroIdempotenciaAluguel
            {
                EmpresaId = aluguel.EmpresaId, UsuarioId = ator, Operacao = Devolucao, Chave = chave, HashComando = hash,
                RevisaoId = revisao.Id, FinalId = final.Id, CriadoEm = agora, AtualizadoEm = agora,
            };
            await repositorio.RegistrarResultadoAsync(aluguel, novoRegistro, token);
            return Resultado(final, revisao, aluguel.Versao);
        }, idempotente: true, ct);
    }

    private static byte[] HashDevolucao(Guid id, ComandoDevolucao comando, string? versaoAceita) =>
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            id, Retorno = RetornoCanonico(comando.RetornoReal), comando.HodometroFinal,
            Litros = LitrosCanonicos(comando.LitrosRestantes), Taxas = (comando.TaxaIds ?? []).Order().ToArray(), versaoAceita,
        }));

    private static string? LitrosCanonicos(string? entrada)
    {
        try { return Decimais.Capacidade(entrada, "litrosRestantes") is { } litros ? Decimais.FormatacaoCapacidade(litros) : entrada; }
        catch (ValidacaoException) { return entrada; }
    }

    private static FechamentoAluguel CapturarFinal(Aluguel aluguel, ContratoSnapshot revisao, EntradaDevolucao entrada,
        DevolucaoPreparada preparada, DateTimeOffset agora)
    {
        var calculo = preparada.Calculo;
        var (diarias, km, parametros, plano, adicionais) = calculo.Parcelas;
        var preco = calculo.PrecoCombustivel;
        var finalId = Guid.NewGuid();
        return new FechamentoAluguel
        {
            Id = finalId, EmpresaId = aluguel.EmpresaId, CriadoEm = agora, AtualizadoEm = agora,
            AluguelId = aluguel.Id, RevisaoId = revisao.Id, VersaoRegra = CalculoDevolucao.VersaoRegra,
            RetornoRealUtc = entrada.RetornoUtc, HodometroInicial = revisao.HodometroInicial, HodometroFinal = entrada.Hodometro,
            CapacidadeLitros = calculo.CapacidadeLitros, LitrosRestantes = calculo.LitrosRestantes, LitrosARepor = calculo.LitrosARepor,
            Diarias = diarias, KmRodados = km, KmCobrados = KmCobrados(parametros.Modalidade, km, plano.FranquiaTotalKm),
            FranquiaTotalKm = plano.FranquiaTotalKm, DiariasBruto = plano.Diarias, QuilometragemBruto = plano.Quilometragem ?? 0,
            ValorPlano = plano.Valor, Combustivel = preco.Tipo, PrecoCombustivelId = preco.VersaoId,
            InicioVigenciaCombustivelUtc = preco.InicioVigenciaUtc, OrdemCombustivel = preco.Ordem, PrecoPorLitro = preco.PrecoPorLitro,
            CombustivelBruto = calculo.Combustivel.Bruto, ValorCombustivel = calculo.Combustivel.Valor,
            MultaBruto = calculo.Multa?.Bruto, ValorMulta = calculo.Multa?.Valor, Total = calculo.Total, Caucao = calculo.Caucao,
            Adicionais = adicionais.Select(x => new ItemFechamentoAluguel
            {
                EmpresaId = aluguel.EmpresaId, CriadoEm = agora, AtualizadoEm = agora, FinalId = finalId,
                TaxaId = x.Item.Categoria == CategoriaAdicional.Taxa ? x.Item.OrigemId : null,
                SeguroId = x.Item.Categoria != CategoriaAdicional.Taxa ? x.Item.OrigemId : null,
                VersaoTaxaAdicional = x.Item.Categoria == CategoriaAdicional.Taxa
                    ? preparada.VersoesTaxas.GetValueOrDefault(x.Item.OrigemId)?.ToArray() : null,
                Nome = x.Item.Nome, Categoria = x.Item.Categoria, Modalidade = x.Item.Modalidade,
                Quantidade = QuantidadeAdicional(x.Item.Categoria, x.Item.Modalidade, diarias),
                Preco = x.Item.Preco, Bruto = x.ValorBruto, Valor = x.Valor,
            }).ToList(),
        };
    }

    private static RespostaComVersao<DevolucaoConcluida> Resultado(FechamentoAluguel final, ContratoSnapshot revisao, byte[] versao) =>
        new(new(final.AluguelId, EstadoAluguel.Concluido, Demonstrativo(final, revisao)), Etag.De(versao));

    private static DemonstrativoDevolucao Demonstrativo(FechamentoAluguel f, ContratoSnapshot r) =>
        new(f.VersaoRegra, HorarioLocacao.ParaSaoPaulo(f.RetornoRealUtc), f.HodometroFinal,
            Decimais.FormatacaoCapacidade(f.LitrosRestantes), f.Diarias, f.KmRodados,
            new(PlanoDto(r.PlanoId, r.ModalidadePlano, r.PrecoDiario, r.PrecoKm, r.FranquiaDiariaKm),
                f.FranquiaTotalKm, f.KmCobrados, Bruto(f.DiariasBruto), Bruto(f.QuilometragemBruto),
                Componente(f.DiariasBruto + f.QuilometragemBruto, f.ValorPlano)),
            f.Adicionais.OrderBy(x => x.Categoria).ThenBy(x => x.VersaoTaxaAdicional is not null).ThenBy(x => x.TaxaId)
                .Select(x => new AdicionalFinal(x.TaxaId ?? x.SeguroId!.Value, x.Nome, x.Categoria, x.Modalidade,
                    x.VersaoTaxaAdicional is not null, x.Quantidade, Decimais.FormatacaoMonetaria(x.Preco),
                    Componente(x.Bruto, x.Valor))).ToArray(),
            new(f.Combustivel, f.PrecoCombustivelId, f.InicioVigenciaCombustivelUtc, f.OrdemCombustivel,
                Decimais.FormatacaoCapacidade(f.LitrosARepor), Decimais.FormatacaoMonetaria(f.PrecoPorLitro),
                Componente(f.CombustivelBruto, f.ValorCombustivel)),
            f.ValorMulta is decimal multa ? Componente(f.MultaBruto!.Value, multa) : null, Valor(f.Total), Valor(f.Caucao));

    private static long KmCobrados(ModalidadePlano modalidade, int km, long? franquia) =>
        modalidade == ModalidadePlano.Livre ? 0 : Math.Max(0, km - (franquia ?? 0));

    private static int QuantidadeAdicional(CategoriaAdicional categoria, ModalidadeTaxa modalidade, int diarias) =>
        categoria == CategoriaAdicional.Taxa && modalidade == ModalidadeTaxa.Fixa ? 1 : diarias;
}
