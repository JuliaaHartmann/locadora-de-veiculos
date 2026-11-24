namespace Locadora.Domain;

public sealed class FechamentoAluguel : EntidadeEmpresa
{
    public Guid AluguelId { get; init; }
    public Guid RevisaoId { get; init; }
    public string VersaoRegra { get; init; } = "";
    public DateTimeOffset RetornoRealUtc { get; init; }
    public int HodometroInicial { get; init; }
    public int HodometroFinal { get; init; }
    public decimal CapacidadeLitros { get; init; }
    public decimal LitrosRestantes { get; init; }
    public decimal LitrosARepor { get; init; }
    public int Diarias { get; init; }
    public int KmRodados { get; init; }
    public long KmCobrados { get; init; }
    public long? FranquiaTotalKm { get; init; }
    public decimal DiariasBruto { get; init; }
    public decimal QuilometragemBruto { get; init; }
    public decimal ValorPlano { get; init; }
    public Guid PrecoCombustivelId { get; init; }
    public Combustivel Combustivel { get; init; }
    public DateTimeOffset InicioVigenciaCombustivelUtc { get; init; }
    public long OrdemCombustivel { get; init; }
    public decimal PrecoPorLitro { get; init; }
    public decimal CombustivelBruto { get; init; }
    public decimal ValorCombustivel { get; init; }
    public decimal? MultaBruto { get; init; }
    public decimal? ValorMulta { get; init; }
    public decimal Total { get; init; }
    public decimal Caucao { get; init; }
    public ICollection<ItemFechamentoAluguel> Adicionais { get; init; } = [];
}

public sealed class ItemFechamentoAluguel : EntidadeEmpresa
{
    public Guid FinalId { get; init; }
    public Guid? TaxaId { get; init; }
    public Guid? SeguroId { get; init; }
    public byte[]? VersaoTaxaAdicional { get; init; }
    public CategoriaAdicional Categoria { get; init; }
    public ModalidadeTaxa Modalidade { get; init; }
    public string Nome { get; init; } = "";
    public int Quantidade { get; init; }
    public decimal Preco { get; init; }
    public decimal Bruto { get; init; }
    public decimal Valor { get; init; }
}
