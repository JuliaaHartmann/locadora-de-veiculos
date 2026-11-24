using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Domain;

namespace Locadora.Application;

public sealed partial class AluguelService
{
    public async Task<RespostaComVersao<PreviaDevolucao>> PreverDevolucaoAsync(Guid id, ComandoDevolucao comando,
        CancellationToken ct)
    {
        var (aluguel, revisao) = await repositorio.LerAtualAsync(id, false, ct) ?? throw new NaoEncontradoException();
        ExigirAbertoParaDevolucao(aluguel);
        var entrada = InterpretarDevolucao(comando);
        var preparada = await PrepararDevolucaoAsync(aluguel, revisao, entrada, ct);
        return new(new(preparada.Versao, Demonstrativo(revisao.PlanoId, entrada, preparada.Calculo)), Etag.De(aluguel.Versao));
    }

    private static void ExigirAbertoParaDevolucao(Aluguel aluguel)
    {
        if (aluguel.Estado != EstadoAluguel.EmAberto)
            throw new ConflitoException("Somente aluguéis em aberto podem ser devolvidos.", "estado");
    }

    private static EntradaDevolucao InterpretarDevolucao(ComandoDevolucao comando)
    {
        var retorno = HorarioLocacao.InterpretarRetorno(comando.RetornoReal, "retornoReal").ToUniversalTime();
        var litros = Decimais.Capacidade(comando.LitrosRestantes, "litrosRestantes")
            ?? throw new ValidacaoException("litrosRestantes", "Informe os litros restantes.");
        var hodometro = comando.HodometroFinal
            ?? throw new ValidacaoException("hodometroFinal", "Informe o hodômetro final.");
        var taxas = comando.TaxaIds ?? [];
        if (taxas.Contains(Guid.Empty) || taxas.Distinct().Count() != taxas.Count)
            throw new ValidacaoException("taxaIds", "Informe taxas válidas, sem repetir uma taxa.");
        return new(retorno, hodometro, litros, taxas.Order().ToArray());
    }

    private async Task<DevolucaoPreparada> PrepararDevolucaoAsync(Aluguel aluguel, ContratoSnapshot revisao,
        EntradaDevolucao entrada, CancellationToken ct, string? versaoEsperada = null)
    {
        var contratados = revisao.Itens.OrderBy(x => x.Categoria).ThenBy(x => x.TaxaId).Select(Parametros).ToArray();
        if (revisao.Itens.Any(x => x.TaxaId is Guid id && entrada.TaxaIds.Contains(id)))
            throw new ValidacaoException("taxaIds", "Uma taxa já contratada não pode ser incluída na devolução.");
        var taxas = await repositorio.LerTaxasAtivasAsync(entrada.TaxaIds, ct);
        if (taxas.Count != entrada.TaxaIds.Length)
        {
            if (versaoEsperada is not null) throw PreviaAlterada();
            throw new NaoEncontradoException();
        }

        var combustivel = await repositorio.LerPrecoNoRetornoAsync(revisao.Combustivel, entrada.RetornoUtc, ct);
        var versao = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aluguel.EmpresaId, AluguelId = aluguel.Id, RevisaoId = revisao.Id, aluguel.Versao,
            Regra = CalculoDevolucao.VersaoRegra, Retorno = entrada.RetornoUtc.UtcTicks, entrada.Hodometro,
            Litros = Decimais.FormatacaoCapacidade(entrada.Litros),
            Taxas = taxas.OrderBy(x => x.Id).Select(x => new { x.Id, x.Versao }),
            Combustivel = combustivel?.Id,
        })));
        if (versaoEsperada is not null && versaoEsperada != versao) throw PreviaAlterada();

        var adicionais = contratados.Where(x => x.Categoria == CategoriaAdicional.Taxa)
            .Concat(taxas.OrderBy(x => x.Id)
                .Select(x => new ParametrosAdicional(x.Id, x.Nome, CategoriaAdicional.Taxa, x.Modalidade, x.Preco)))
            .Concat(contratados.Where(x => x.Categoria != CategoriaAdicional.Taxa)).ToArray();
        var calculo = CalculoLocacao.CalcularDevolucao(aluguel.SaidaUtc, revisao.RetornoPrevistoUtc, entrada.RetornoUtc,
            revisao.HodometroInicial, entrada.Hodometro,
            new(revisao.ModalidadePlano, revisao.PrecoDiario, revisao.PrecoKm, revisao.FranquiaDiariaKm),
            adicionais, revisao.Combustivel, revisao.CapacidadeLitros, entrada.Litros,
            combustivel is null ? null : new(combustivel.Id, combustivel.Tipo, combustivel.Preco,
                combustivel.InicioVigenciaUtc, combustivel.Ordem));
        return new(versao, calculo, taxas.ToDictionary(x => x.Id, x => x.Versao));
    }

    private static ConflitoException PreviaAlterada() =>
        new("O cálculo aceito não está mais vigente. Calcule e aceite uma nova prévia.", "versaoAceita");

    private static DemonstrativoDevolucao Demonstrativo(Guid planoId, EntradaDevolucao entrada, CalculoDevolucao calculo)
    {
        var (diarias, km, parametros, parcelaPlano, adicionais) = calculo.Parcelas;
        var preco = calculo.PrecoCombustivel;
        var kmCobrados = KmCobrados(parametros.Modalidade, km, parcelaPlano.FranquiaTotalKm);
        return new(CalculoDevolucao.VersaoRegra, HorarioLocacao.ParaSaoPaulo(entrada.RetornoUtc), entrada.Hodometro,
            Decimais.FormatacaoCapacidade(entrada.Litros), diarias, km,
            new(PlanoDto(planoId, parametros.Modalidade, parametros.PrecoDiario, parametros.PrecoKm, parametros.FranquiaDiariaKm),
                parcelaPlano.FranquiaTotalKm, kmCobrados, Bruto(parcelaPlano.Diarias), Bruto(parcelaPlano.Quilometragem ?? 0),
                Componente(parcelaPlano.Diarias + (parcelaPlano.Quilometragem ?? 0), parcelaPlano.Valor)),
            adicionais.Select(x => new AdicionalFinal(x.Item.OrigemId, x.Item.Nome, x.Item.Categoria, x.Item.Modalidade,
                x.Item.Categoria == CategoriaAdicional.Taxa && entrada.TaxaIds.Contains(x.Item.OrigemId),
                QuantidadeAdicional(x.Item.Categoria, x.Item.Modalidade, diarias),
                Decimais.FormatacaoMonetaria(x.Item.Preco), Componente(x.ValorBruto, x.Valor))).ToArray(),
            new(preco.Tipo, preco.VersaoId, preco.InicioVigenciaUtc, preco.Ordem,
                Decimais.FormatacaoCapacidade(calculo.LitrosARepor), Decimais.FormatacaoMonetaria(preco.PrecoPorLitro),
                Componente(calculo.Combustivel.Bruto, calculo.Combustivel.Valor)),
            calculo.Multa is { } multa ? Componente(multa.Bruto, multa.Valor) : null, Valor(calculo.Total), Valor(calculo.Caucao));
    }

    private static string Bruto(decimal valor) => valor.ToString("0.#########", CultureInfo.InvariantCulture);
    private static ParcelaMonetaria Componente(decimal bruto, decimal valor) => new(Bruto(bruto), Valor(valor));

    private sealed record EntradaDevolucao(DateTimeOffset RetornoUtc, int Hodometro, decimal Litros, Guid[] TaxaIds);
    private sealed record DevolucaoPreparada(string Versao, CalculoDevolucao Calculo, IReadOnlyDictionary<Guid, byte[]> VersoesTaxas);
}
