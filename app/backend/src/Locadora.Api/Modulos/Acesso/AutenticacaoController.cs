using System.Security.Claims;
using Locadora.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Locadora.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AutenticacaoController(IAutenticacao autenticacao) : ControllerBase
{
    [HttpPost("registro")]
    [EnableRateLimiting("registro")]
    public async Task<IActionResult> Registrar(RegistroRequisicao requisicao, CancellationToken ct)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created,
                await autenticacao.RegistrarAsync(requisicao.Usuario, requisicao.Email, requisicao.Senha, ct));
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Requisição inválida", detail: ex.Message); }
        catch (ConflitoRegistroException) { return Problem(statusCode: 409, title: "Conflito", detail: "Usuário indisponível."); }
    }

    [HttpPost("session/login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await autenticacao.LoginAsync(requisicao.Usuario, requisicao.Senha, ct);
        return resultado is null ? Problem(statusCode: 401, title: "Credenciais inválidas") : Ok(resultado);
    }

    [Authorize]
    [HttpGet("session")]
    public async Task<IActionResult> Sessao(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sid"), out var id)) return Unauthorized();
        var sessao = await autenticacao.ObterSessaoAsync(id, ct);
        return sessao is null ? Unauthorized() : Ok(new { ativa = true, sessaoId = id,
            usuarioId = sessao.UsuarioId, empresaId = sessao.EmpresaId, expiraEm = sessao.ExpiraEm });
    }

    [HttpPost("session/refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await autenticacao.RenovarAsync(requisicao.RefreshToken, ct);
        return resultado is null ? Problem(statusCode: 401, title: "Sessão inválida") : Ok(resultado);
    }

    [Authorize]
    [HttpPost("session/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Guid.TryParse(User.FindFirstValue("sid"), out var id)) await autenticacao.RevogarAsync(id, ct);
        return NoContent();
    }
}
