using System.Text.Json.Serialization;
using Locadora.Domain;

namespace Locadora.Application;

public sealed record ComandoAluguel(Guid ClienteId, Guid CondutorId, Guid VeiculoId, Guid PlanoId,
    string? RetornoPrevisto, int? HodometroInicial, IReadOnlyList<Guid>? TaxaIds, IReadOnlyList<TipoSeguro>? Seguros);

public sealed record PlanoContratado(Guid Id, ModalidadePlano Modalidade, string PrecoDiario, string? PrecoKm,
    int? FranquiaDiariaKm);

public sealed record ComponenteContratado(Guid OrigemId, CategoriaAdicional Categoria, string Nome,
    ModalidadeTaxa Modalidade, string Preco, string Valor);

public sealed record DemonstrativoEstimado(int Diarias, PlanoContratado Plano, string ValorPlano,
    long? FranquiaTotalKm, bool QuilometragemPendente, IReadOnlyList<ComponenteContratado> Adicionais,
    string ParcelasConhecidas, string Caucao, bool Incompleta);

public sealed record PreviaAluguel(DateTimeOffset SaidaReferencia, DateTimeOffset RetornoPrevisto,
    DemonstrativoEstimado Estimativa);

public sealed record ContratanteLocacao(Guid Id, string Nome, TipoCliente Tipo, string DocumentoMascarado);
public sealed record CondutorLocacao(Guid Id, string Nome, DateOnly ValidadeCnh);
public sealed record VeiculoLocacao(Guid Id, string Placa, string Marca, string Modelo, Combustivel Combustivel,
    string CapacidadeLitros);
public sealed record GrupoLocacao(Guid Id, string Nome);
public sealed record AluguelDetalhe(Guid Id, EstadoAluguel Estado, ContratanteLocacao Cliente, CondutorLocacao Condutor,
    VeiculoLocacao Veiculo, GrupoLocacao Grupo, DateTimeOffset Saida, DateTimeOffset RetornoPrevisto,
    int HodometroInicial, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DemonstrativoEstimado? Estimativa,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DemonstrativoDevolucao? Final = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Oculto = false);

public sealed record AluguelResumo(Guid Id, EstadoAluguel Estado, string Condutor, Guid VeiculoId, string Placa,
    string Marca, string Modelo, ModalidadePlano Plano, DateTimeOffset Saida, DateTimeOffset RetornoPrevisto,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParcelasConhecidas,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TotalFinal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Oculto = false);

public sealed record LinhaAluguel(Aluguel Aluguel, ContratoSnapshot Revisao, decimal? TotalFinal);
public sealed record FiltroAluguel(Guid? VeiculoId, DateTimeOffset? InicioUtc, DateTimeOffset? FimExclusivoUtc,
    EstadoAluguel? Estado, bool IncluirOcultos);

public sealed record ReferenciasLocacao(Cliente Cliente, Condutor Condutor, Veiculo Veiculo, Plano Plano,
    IReadOnlyList<TaxaServico> Taxas, IReadOnlyList<SeguroConfiguracao> Seguros);

public interface IRepositorioAlugueis
{
    Task<ReferenciasLocacao> LerReferenciasAsync(ComandoAluguel comando, CancellationToken ct);
    Task<bool> VeiculoOcupadoAsync(Guid veiculoId, Guid? aluguelAtual, CancellationToken ct);
    Task<T> ExecutarEscritaAsync<T>(Func<CancellationToken, Task<T>> operacao, bool idempotente, CancellationToken ct);
    Task<RegistroIdempotenciaAluguel?> LerChaveAsync(Guid usuarioId, string operacao, string chave, CancellationToken ct);
    Task<(Aluguel Aluguel, ContratoSnapshot Revisao)> LerResultadoOriginalAsync(Guid revisaoId, CancellationToken ct);
    Task<(Aluguel Aluguel, ContratoSnapshot Revisao)?> LerAtualAsync(Guid aluguelId, bool paraEdicao, CancellationToken ct,
        bool incluirOcultos = false);
    Task<IReadOnlyList<TaxaServico>> LerTaxasAtivasAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<PrecoCombustivel?> LerPrecoNoRetornoAsync(Combustivel tipo, DateTimeOffset retornoUtc, CancellationToken ct);
    Task<(FechamentoAluguel Final, ContratoSnapshot Revisao)> LerFinalOriginalAsync(Guid finalId, CancellationToken ct);
    Task<FechamentoAluguel> LerFinalDaRevisaoAsync(Guid aluguelId, Guid revisaoId, CancellationToken ct);
    Task<Pagina<LinhaAluguel>> ListarAsync(FiltroAluguel filtro, int pagina, int tamanho, CancellationToken ct);
    void Adicionar(Aluguel aluguel);
    void Adicionar(ContratoSnapshot revisao);
    void Adicionar(FechamentoAluguel final);
    Task RegistrarResultadoAsync(Aluguel aluguel, RegistroIdempotenciaAluguel registro, CancellationToken ct);
}
