using Locadora.Domain;

namespace Locadora.Application;

public sealed record PlanoResumo(
    Guid Id,
    Guid GrupoId,
    string GrupoNome,
    ModalidadePlano Modalidade,
    string PrecoDiario,
    string? PrecoKm,
    int? FranquiaDiariaKm);

public sealed record GrupoResumo(Guid Id, string Nome, IReadOnlyList<PlanoResumo> Planos);

public sealed record GrupoDetalhe(Guid Id, string Nome, IReadOnlyList<PlanoResumo> Planos);

public sealed record ComandoGrupo(string? Nome);

public sealed record ComandoPlano(
    Guid GrupoId,
    ModalidadePlano Modalidade,
    string? PrecoDiario,
    string? PrecoKm,
    int? FranquiaDiariaKm);

public sealed record ComandoEdicaoPlano(string? PrecoDiario, string? PrecoKm, int? FranquiaDiariaKm);

public sealed record VeiculoResumo(
    Guid Id,
    Guid GrupoId,
    string GrupoNome,
    string Placa,
    string Marca,
    string Modelo,
    string Cor,
    int Ano,
    Combustivel Combustivel,
    string CapacidadeLitros);

public sealed record ComandoVeiculo(
    Guid GrupoId,
    string? Placa,
    string? Marca,
    string? Modelo,
    string? Cor,
    int? Ano,
    Combustivel Combustivel,
    string? CapacidadeLitros);

public interface IGrupoService
{
    Task<Pagina<GrupoResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<GrupoDetalhe>?> ObterAsync(Guid id, CancellationToken ct);
    Task<RespostaComVersao<GrupoDetalhe>> CriarAsync(ComandoGrupo comando, CancellationToken ct);
    Task<RespostaComVersao<GrupoDetalhe>> EditarAsync(Guid id, ComandoGrupo comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
}

public interface IPlanoService
{
    Task<Pagina<PlanoResumo>> ListarAsync(Guid? grupoId, int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<PlanoResumo>?> ObterAsync(Guid id, CancellationToken ct);
    Task<RespostaComVersao<PlanoResumo>> CriarAsync(ComandoPlano comando, CancellationToken ct);
    Task<RespostaComVersao<PlanoResumo>> EditarAsync(Guid id, ComandoEdicaoPlano comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
}

public interface IVeiculoService
{
    Task<Pagina<VeiculoResumo>> ListarAsync(Guid? grupoId, int? page, int? pageSize, CancellationToken ct,
        bool disponiveis = false, Guid? aluguelAtualId = null);
    Task<RespostaComVersao<VeiculoResumo>?> ObterAsync(Guid id, CancellationToken ct, bool paraEdicao = false);
    Task<RespostaComVersao<VeiculoResumo>> CriarAsync(ComandoVeiculo comando, CancellationToken ct);
    Task<RespostaComVersao<VeiculoResumo>> EditarAsync(Guid id, ComandoVeiculo comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
}
