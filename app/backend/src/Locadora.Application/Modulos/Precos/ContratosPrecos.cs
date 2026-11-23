using Locadora.Domain;

namespace Locadora.Application;

public sealed record TaxaResumo(Guid Id, string Nome, string Preco, ModalidadeTaxa Modalidade);

public sealed record ComandoTaxa(string? Nome, string? Preco, ModalidadeTaxa Modalidade);

public sealed record SeguroEstado(TipoSeguro Tipo, bool Configurado, string? PrecoDiario);

public sealed record ComandoSeguro(string? PrecoDiario);

public sealed record CombustivelEstado(
    Combustivel Tipo,
    bool Configurado,
    Guid? Id,
    string? Preco,
    DateTimeOffset? InicioVigenciaUtc,
    long? Ordem);

public sealed record CombustivelVersao(
    Guid Id,
    Combustivel Tipo,
    string Preco,
    DateTimeOffset InicioVigenciaUtc,
    long Ordem);

public sealed record ComandoCombustivel(string? Preco);

public interface ITaxaService
{
    Task<Pagina<TaxaResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<TaxaResumo>?> ObterAsync(Guid id, CancellationToken ct, bool paraEdicao = false);
    Task<RespostaComVersao<TaxaResumo>> CriarAsync(ComandoTaxa comando, CancellationToken ct);
    Task<RespostaComVersao<TaxaResumo>> EditarAsync(Guid id, ComandoTaxa comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
}

public interface ISeguroService
{
    Task<RespostaComVersao<SeguroEstado>> ObterAsync(TipoSeguro tipo, CancellationToken ct);
    Task<RespostaComVersao<SeguroEstado>> ConfigurarAsync(TipoSeguro tipo, ComandoSeguro comando, string? ifMatch, CancellationToken ct);
}

public interface ICombustivelService
{
    Task<RespostaComVersao<CombustivelEstado>> ObterAsync(Combustivel tipo, CancellationToken ct);
    Task<Pagina<CombustivelVersao>> HistoricoAsync(Combustivel tipo, int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<CombustivelEstado>> ConfigurarAsync(Combustivel tipo, ComandoCombustivel comando, string? ifMatch, CancellationToken ct);
}
