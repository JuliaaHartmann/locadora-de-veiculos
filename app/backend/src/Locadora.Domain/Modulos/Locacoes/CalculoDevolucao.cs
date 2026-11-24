namespace Locadora.Domain;

public sealed record PrecoReposicao(Guid VersaoId, Combustivel Tipo, decimal PrecoPorLitro,
    DateTimeOffset InicioVigenciaUtc, long Ordem);

public sealed record ComponenteMonetario(decimal Bruto, decimal Valor)
{
    public static ComponenteMonetario De(decimal bruto) => new(bruto, CalculoLocacao.Arredondar(bruto));
}

public sealed record CalculoDevolucao(ParcelasEfetivas Parcelas, decimal CapacidadeLitros, decimal LitrosRestantes,
    decimal LitrosARepor, PrecoReposicao PrecoCombustivel, ComponenteMonetario Combustivel,
    ComponenteMonetario? Multa, decimal Total, decimal Caucao)
{
    public const string VersaoRegra = "RN-01-09/v1";
}

public static partial class CalculoLocacao
{
    public const decimal MaximoTotalFinal = 99999999999999999.99m;

    public static CalculoDevolucao CalcularDevolucao(DateTimeOffset saida, DateTimeOffset retornoPrevisto,
        DateTimeOffset retornoReal, int hodometroInicial, int hodometroFinal, ParametrosPlano plano,
        IReadOnlyList<ParametrosAdicional> adicionais, Combustivel tipo, decimal capacidade, decimal litrosRestantes,
        PrecoReposicao? preco)
    {
        var diarias = Diarias(saida, retornoReal, "retornoReal");
        var km = KmEfetivos(hodometroInicial, hodometroFinal);
        if (capacidade <= 0 || capacidade > Decimais.MaximoCapacidade || decimal.Round(capacidade, 3) != capacidade ||
            litrosRestantes < 0 || litrosRestantes > capacidade || decimal.Round(litrosRestantes, 3) != litrosRestantes)
            throw new ValidacaoException("litrosRestantes",
                "Informe os litros restantes entre zero e a capacidade contratada, com até três casas decimais.");

        if (preco is null || preco.Tipo != tipo || preco.InicioVigenciaUtc > retornoReal)
            throw new ValidacaoException("combustivel",
                $"Não há preço de {tipo} vigente no retorno real. Confira a configuração em Preços.");

        var kmCobrados = plano.Modalidade switch
        {
            ModalidadePlano.Diario => km,
            ModalidadePlano.Controlado => KmCobrados(km, (long)plano.FranquiaDiariaKm!.Value * diarias),
            _ => 0,
        };
        var litrosARepor = capacidade - litrosRestantes;
        ValidarProduto(plano.PrecoDiario, diarias);
        if (plano.PrecoKm is decimal precoKm) ValidarProduto(precoKm, kmCobrados);
        foreach (var item in adicionais) ValidarProduto(item.Preco, Quantidade(item, diarias));
        ValidarProduto(preco.PrecoPorLitro, litrosARepor);

        var parcelas = CalcularParcelasEfetivas(diarias, km, plano, adicionais);
        var combustivel = ComponenteMonetario.De(litrosARepor * preco.PrecoPorLitro);
        var multa = retornoReal > retornoPrevisto ? ComponenteMonetario.De(parcelas.Plano.Valor * .10m) : null;
        var total = 0m;
        Somar(ref total, parcelas.Plano.Valor);
        foreach (var item in parcelas.Adicionais) Somar(ref total, item.Valor);
        Somar(ref total, combustivel.Valor);
        if (multa is not null) Somar(ref total, multa.Valor);

        return new(parcelas, capacidade, litrosRestantes, litrosARepor, preco, combustivel, multa, total, Caucao);
    }

    private static void ValidarProduto(decimal preco, decimal quantidade)
    {
        // A margem permite arredondar na fronteira; Somar valida o valor monetário efetivamente persistido.
        if (preco < 0 || preco > Decimais.MaximoMonetario || decimal.Round(preco, 6) != preco ||
            (quantidade >= 1 && preco > (MaximoTotalFinal + 1) / quantidade))
            throw ValorForaDoIntervalo();
    }

    private static void Somar(ref decimal total, decimal valor)
    {
        if (valor > MaximoTotalFinal - total)
            throw ValorForaDoIntervalo();
        total += valor;
    }

    private static ValidacaoException ValorForaDoIntervalo() =>
        new("retornoReal", "O valor do demonstrativo final excede o intervalo suportado.");
}
