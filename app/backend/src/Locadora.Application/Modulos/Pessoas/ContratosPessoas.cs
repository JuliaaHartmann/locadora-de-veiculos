using Locadora.Domain;

namespace Locadora.Application;

public sealed record EnderecoContrato(string Estado, string Cidade, string Bairro, string Rua, string Numero);

public sealed record ClienteResumo(
    Guid Id,
    TipoCliente Tipo,
    string Nome,
    string Telefone,
    string? Email,
    string? Cpf,
    string? Rg,
    string? Cnh,
    string? Cnpj);

public sealed record ClienteVinculado(Guid Id, string Nome, string? Cnpj);

public sealed record ClienteDetalhe(
    Guid Id,
    TipoCliente Tipo,
    string Nome,
    string Telefone,
    string? Email,
    string? Cpf,
    string? Rg,
    string? Cnh,
    string? Cnpj,
    EnderecoContrato Endereco,
    IReadOnlyList<ClienteVinculado> VinculosPj);

public sealed record CondutorResumo(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    string Nome,
    string Email,
    string Cpf,
    string Cnh,
    DateOnly ValidadeCnh,
    string Telefone);

public sealed record ComandoCliente(
    TipoCliente Tipo,
    string? Nome,
    string? Cpf,
    string? Rg,
    string? Cnh,
    string? Cnpj,
    string? Telefone,
    string? Email,
    string? Estado,
    string? Cidade,
    string? Bairro,
    string? Rua,
    string? Numero);

public sealed record ComandoCondutor(
    Guid ClienteId,
    string? Nome,
    string? Email,
    string? Cpf,
    string? Cnh,
    DateOnly? ValidadeCnh,
    string? Telefone);

public sealed record ComandoVinculo(Guid ClientePjId);

public interface IClienteService
{
    Task<Pagina<ClienteResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<ClienteDetalhe>?> ObterAsync(Guid id, CancellationToken ct);
    Task<RespostaComVersao<ClienteDetalhe>> CriarAsync(ComandoCliente comando, CancellationToken ct);
    Task<RespostaComVersao<ClienteDetalhe>> EditarAsync(Guid id, ComandoCliente comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
    Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> ListarVinculosAsync(Guid clientePfId, CancellationToken ct);
    Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> AdicionarVinculoAsync(Guid clientePfId, ComandoVinculo comando, string? ifMatch, CancellationToken ct);
    Task<RespostaComVersao<IReadOnlyList<ClienteVinculado>>> RemoverVinculoAsync(Guid clientePfId, Guid clientePjId, string? ifMatch, CancellationToken ct);
}

public interface ICondutorService
{
    Task<Pagina<CondutorResumo>> ListarAsync(int? page, int? pageSize, CancellationToken ct);
    Task<RespostaComVersao<CondutorResumo>?> ObterAsync(Guid id, CancellationToken ct);
    Task<RespostaComVersao<CondutorResumo>> CriarAsync(ComandoCondutor comando, CancellationToken ct);
    Task<RespostaComVersao<CondutorResumo>> EditarAsync(Guid id, ComandoCondutor comando, string? ifMatch, CancellationToken ct);
    Task ExcluirAsync(Guid id, string? ifMatch, CancellationToken ct);
}
