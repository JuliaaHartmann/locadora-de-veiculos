namespace Locadora.Domain;

public sealed record ParametrosPlano(ModalidadePlano Modalidade, decimal PrecoDiario, decimal? PrecoKm,
    int? FranquiaDiariaKm)
{
    public static ParametrosPlano De(Plano plano) =>
        new(plano.Modalidade, plano.PrecoDiario, plano.PrecoKm, plano.FranquiaDiariaKm);
}

public sealed record ParcelaPlano(decimal Diarias, decimal? Quilometragem, bool QuilometragemPendente,
    long? FranquiaTotalKm, decimal Valor);

public enum CategoriaAdicional
{
    Taxa,
    SeguroCliente,
    SeguroTerceiros,
}

public sealed record ParametrosAdicional(Guid OrigemId, string Nome, CategoriaAdicional Categoria,
    ModalidadeTaxa Modalidade, decimal Preco);

public sealed record ParcelaAdicional(ParametrosAdicional Item, decimal ValorBruto, decimal Valor);

public sealed record ParcelasEfetivas(int Diarias, int KmRodados, ParametrosPlano Parametros, ParcelaPlano Plano,
    IReadOnlyList<ParcelaAdicional> Adicionais);

public sealed record EstimativaLocacao(int Diarias, ParametrosPlano Parametros, ParcelaPlano Plano,
    IReadOnlyList<ParcelaAdicional> Adicionais, decimal ParcelasConhecidas, decimal Caucao)
{
    // Combustível e eventual atraso só podem ser apurados na devolução, inclusive no plano Livre.
    public bool Incompleta => true;
}

public static partial class CalculoLocacao
{
    public const decimal Caucao = 1000m;

    public static int Diarias(DateTimeOffset saida, DateTimeOffset retorno) =>
        Diarias(saida, retorno, "retornoPrevisto");

    private static int Diarias(DateTimeOffset saida, DateTimeOffset retorno, string campo)
    {
        long ticks = (retorno - saida).Ticks;
        if (ticks < 0)
            throw new ValidacaoException(campo, "O retorno não pode preceder a saída.");
        return ticks == 0 ? 1 : checked((int)((ticks - 1) / TimeSpan.TicksPerDay + 1));
    }

    public static ParcelaPlano Plano(ParametrosPlano plano, int diarias, int? kmRodados)
    {
        if (diarias < 1 || kmRodados < 0)
            throw new ArgumentOutOfRangeException(diarias < 1 ? nameof(diarias) : nameof(kmRodados));

        decimal baseDiarias = plano.PrecoDiario * diarias;
        long? franquia = plano.Modalidade == ModalidadePlano.Controlado ? (long)plano.FranquiaDiariaKm!.Value * diarias : null;
        decimal? km = plano.Modalidade switch
        {
            ModalidadePlano.Diario or ModalidadePlano.Controlado when kmRodados.HasValue =>
                KmCobrados(kmRodados.Value, franquia) * plano.PrecoKm!.Value,
            ModalidadePlano.Diario or ModalidadePlano.Controlado or ModalidadePlano.Livre => null,
            _ => throw new ArgumentOutOfRangeException(nameof(plano)),
        };
        return new(baseDiarias, km, plano.Modalidade != ModalidadePlano.Livre && !kmRodados.HasValue,
            franquia, Arredondar(baseDiarias + (km ?? 0m)));
    }

    public static decimal Arredondar(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static EstimativaLocacao Estimar(DateTimeOffset saida, DateTimeOffset retorno,
        ParametrosPlano plano, IReadOnlyList<ParametrosAdicional> adicionais)
    {
        var diarias = Diarias(saida, retorno);
        var parcelaPlano = Plano(plano, diarias, null);
        var parcelas = CalcularAdicionais(adicionais, diarias);
        return new(diarias, plano, parcelaPlano, parcelas, parcelaPlano.Valor + parcelas.Sum(x => x.Valor), Caucao);
    }

    private static long KmCobrados(int km, long? franquia) => Math.Max(0, km - (franquia ?? 0));

    public static ParcelasEfetivas CalcularParcelasEfetivas(DateTimeOffset saida, DateTimeOffset retornoReal,
        int hodometroInicial, int hodometroFinal, ParametrosPlano plano, IReadOnlyList<ParametrosAdicional> adicionais)
    {
        var diarias = Diarias(saida, retornoReal, "retornoReal");
        var km = KmEfetivos(hodometroInicial, hodometroFinal);
        return CalcularParcelasEfetivas(diarias, km, plano, adicionais);
    }

    private static ParcelasEfetivas CalcularParcelasEfetivas(int diarias, int km, ParametrosPlano plano,
        IReadOnlyList<ParametrosAdicional> adicionais) =>
        new(diarias, km, plano, Plano(plano, diarias, km), CalcularAdicionais(adicionais, diarias));

    private static int KmEfetivos(int inicial, int final)
    {
        if (inicial < 0 || final < inicial)
            throw new ValidacaoException("hodometroFinal", "O hodômetro final deve ser igual ou superior ao inicial.");
        return final - inicial;
    }

    private static int Quantidade(ParametrosAdicional item, int diarias) =>
        item.Categoria == CategoriaAdicional.Taxa && item.Modalidade == ModalidadeTaxa.Fixa ? 1 : diarias;

    private static ParcelaAdicional[] CalcularAdicionais(IReadOnlyList<ParametrosAdicional> adicionais, int diarias) =>
        adicionais.Select(item =>
        {
            var bruto = item.Preco * Quantidade(item, diarias);
            return new ParcelaAdicional(item, bruto, Arredondar(bruto));
        }).ToArray();
}
