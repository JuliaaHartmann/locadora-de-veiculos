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

public sealed record EstimativaLocacao(int Diarias, ParametrosPlano Parametros, ParcelaPlano Plano,
    IReadOnlyList<ParcelaAdicional> Adicionais, decimal ParcelasConhecidas, decimal Caucao)
{
    // Combustível e eventual atraso só podem ser apurados na devolução, inclusive no plano Livre.
    public bool Incompleta => true;
}

public static class CalculoLocacao
{
    public const decimal Caucao = 1000m;

    public static int Diarias(DateTimeOffset saida, DateTimeOffset retorno)
    {
        long ticks = (retorno - saida).Ticks;
        if (ticks < 0)
            throw new ValidacaoException("retornoPrevisto", "O retorno não pode preceder a saída.");
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
            ModalidadePlano.Diario when kmRodados.HasValue => kmRodados.Value * plano.PrecoKm!.Value,
            ModalidadePlano.Controlado when kmRodados.HasValue => Math.Max(0, kmRodados.Value - franquia!.Value) * plano.PrecoKm!.Value,
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
        var parcelas = adicionais.Select(item =>
        {
            var bruto = item.Preco * (item.Categoria != CategoriaAdicional.Taxa ||
                item.Modalidade == ModalidadeTaxa.Diaria ? diarias : 1);
            return new ParcelaAdicional(item, bruto, Arredondar(bruto));
        }).ToArray();
        return new(diarias, plano, parcelaPlano, parcelas, parcelaPlano.Valor + parcelas.Sum(x => x.Valor), Caucao);
    }
}
