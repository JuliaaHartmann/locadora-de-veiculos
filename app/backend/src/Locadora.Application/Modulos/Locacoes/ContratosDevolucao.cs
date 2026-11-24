using Locadora.Domain;

namespace Locadora.Application;

public sealed record ComandoDevolucao(string? RetornoReal, int? HodometroFinal, string? LitrosRestantes,
    IReadOnlyList<Guid>? TaxaIds);

public sealed record ParcelaMonetaria(string Bruto, string Valor);
public sealed record PlanoFinal(PlanoContratado Parametros, long? FranquiaTotalKm, long KmCobrados,
    string DiariasBruto, string QuilometragemBruto, ParcelaMonetaria Subtotal);
public sealed record AdicionalFinal(Guid OrigemId, string Nome, CategoriaAdicional Categoria, ModalidadeTaxa Modalidade,
    bool IncluidoNoRetorno, int Quantidade, string Preco, ParcelaMonetaria Parcela);
public sealed record CombustivelFinal(Combustivel Tipo, Guid VersaoId, DateTimeOffset InicioVigenciaUtc, long Ordem,
    string LitrosARepor, string PrecoPorLitro, ParcelaMonetaria Parcela);
public sealed record DemonstrativoDevolucao(string VersaoRegra, DateTimeOffset RetornoReal, int HodometroFinal,
    string LitrosRestantes, int Diarias, int KmRodados, PlanoFinal Plano, IReadOnlyList<AdicionalFinal> Adicionais,
    CombustivelFinal Combustivel, ParcelaMonetaria? Multa, string Total, string Caucao);
public sealed record PreviaDevolucao(string VersaoAceita, DemonstrativoDevolucao Demonstrativo);
public sealed record DevolucaoConcluida(Guid Id, EstadoAluguel Estado, DemonstrativoDevolucao Final);

public sealed class FalhaGravacaoDevolucaoException(Exception causa)
    : Exception("Não foi possível gravar a devolução. O aluguel continua em aberto e o veículo permanece ocupado.", causa);
