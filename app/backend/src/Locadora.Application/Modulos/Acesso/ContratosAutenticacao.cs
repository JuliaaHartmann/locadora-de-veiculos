using Locadora.Domain;

namespace Locadora.Application;

public interface IAutenticacao
{
    Task<ResultadoRegistro> RegistrarAsync(string usuario, string email, string senha, CancellationToken cancellationToken);
    Task<ResultadoAutenticacao?> LoginAsync(string usuario, string senha, CancellationToken cancellationToken);
    Task<ResultadoAutenticacao?> RenovarAsync(string refresh, CancellationToken cancellationToken);
    Task<Sessao?> ObterSessaoAsync(Guid sessaoId, CancellationToken cancellationToken);
    Task<Sessao?> ValidarSessaoAsync(Guid sessaoId, Guid usuarioId, Guid empresaId, string versao, bool permitirRevogada, CancellationToken cancellationToken);
    Task RevogarAsync(Guid sessaoId, CancellationToken cancellationToken);
}

public sealed record ResultadoAutenticacao(string AccessToken, string RefreshToken, Guid SessaoId, DateTimeOffset AccessExpiraEm);
public sealed record ResultadoRegistro(Guid EmpresaId, Guid UsuarioId);

public sealed class ConflitoRegistroException() : Exception("Usuário indisponível.");
