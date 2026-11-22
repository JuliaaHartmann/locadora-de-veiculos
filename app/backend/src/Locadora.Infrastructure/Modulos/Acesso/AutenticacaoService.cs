using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.Infrastructure;

public sealed class AutenticacaoService(
    LocadoraDbContext db,
    UserManager<UsuarioIdentity> usuarios,
    ConfiguracaoJwt jwt,
    IRelogio relogio) : IAutenticacao
{
    public async Task<ResultadoRegistro> RegistrarAsync(string usuario, string email, string senha, CancellationToken ct)
    {
        if (!PoliticaSenha.EhValida(senha)) throw new ArgumentException("Senha inválida.");
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var empresa = new Empresa { CriadaEm = relogio.Agora };
        db.Empresas.Add(empresa);
        var conta = new UsuarioIdentity { UserName = usuario.Trim(), Email = email.Trim(), EmpresaId = empresa.Id };
        try
        {
            var resultado = await usuarios.CreateAsync(conta, senha);
            if (!resultado.Succeeded)
            {
                if (resultado.Errors.Any(x => x.Code == "DuplicateUserName"))
                    throw new ConflitoRegistroException();
                throw new ArgumentException("Dados de registro inválidos.");
            }
            Auditar(empresa.Id, conta.Id, "Registro");
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            return new(empresa.Id, conta.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflitoRegistroException();
        }
    }

    public async Task<ResultadoAutenticacao?> LoginAsync(string usuario, string senha, CancellationToken ct)
    {
        var candidata = await usuarios.FindByNameAsync(usuario);
        if (candidata is null) return null;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await BloquearEmpresa(candidata.EmpresaId, ct);
        await db.Entry(candidata).ReloadAsync(ct);
        if (await usuarios.IsLockedOutAsync(candidata)) return null;
        if (!await usuarios.CheckPasswordAsync(candidata, senha))
        {
            await usuarios.AccessFailedAsync(candidata);
            await transacao.CommitAsync(ct);
            return null;
        }
        await usuarios.ResetAccessFailedCountAsync(candidata);
        var sessao = new Sessao
        {
            UsuarioId = candidata.Id, EmpresaId = candidata.EmpresaId,
            CriadaEm = relogio.Agora, ExpiraEm = relogio.Agora.Add(PoliticaSessao.SessaoAbsoluta),
            VersaoAcesso = candidata.SecurityStamp!
        };
        db.Sessoes.Add(sessao);
        var tokens = EmitirTokens(sessao);
        Auditar(sessao.EmpresaId, sessao.UsuarioId, "Login");
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return tokens;
    }

    public async Task<ResultadoAutenticacao?> RenovarAsync(string refresh, CancellationToken ct)
    {
        var hash = Hash(refresh);
        // A leitura inicial só localiza o lock. Toda autorização e consumo ocorrem depois dele.
        var referencia = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.Hash == hash, ct);
        if (referencia is null) return null;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await BloquearEmpresa(referencia.EmpresaId, ct);
        var registro = await db.RefreshTokens.SingleAsync(x => x.Id == referencia.Id, ct);
        var sessao = await db.Sessoes.SingleOrDefaultAsync(x => x.Id == registro.SessaoId, ct);
        if (sessao is null || !await ContaValida(sessao, ct) || sessao.RevogadaEm is not null ||
            PoliticaSessao.Expirada(relogio.Agora, sessao.ExpiraEm)) return null;
        if (registro.ConsumidoEm is not null || registro.RevogadoEm is not null)
        {
            await RevogarFamilia(sessao, "ReusoRefresh", ct);
            await transacao.CommitAsync(ct);
            return null;
        }
        registro.ConsumidoEm = relogio.Agora;
        var tokens = EmitirTokens(sessao);
        Auditar(sessao.EmpresaId, sessao.UsuarioId, "Refresh");
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return tokens;
    }

    public async Task<Sessao?> ObterSessaoAsync(Guid sessaoId, CancellationToken ct)
    {
        var sessao = await db.Sessoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessaoId, ct);
        return sessao is not null && sessao.RevogadaEm is null && sessao.ExpiraEm > relogio.Agora &&
            await ContaValida(sessao, ct) ? sessao : null;
    }

    public async Task<Sessao?> ValidarSessaoAsync(Guid sessaoId, Guid usuarioId, Guid empresaId,
        string versao, bool permitirRevogada, CancellationToken ct)
    {
        var sessao = await db.Sessoes.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == sessaoId && x.UsuarioId == usuarioId && x.EmpresaId == empresaId && x.VersaoAcesso == versao, ct);
        return sessao is not null && sessao.ExpiraEm > relogio.Agora &&
            (permitirRevogada || sessao.RevogadaEm is null) && await ContaValida(sessao, ct) ? sessao : null;
    }

    public async Task RevogarAsync(Guid sessaoId, CancellationToken ct)
    {
        var referencia = await db.Sessoes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessaoId, ct);
        if (referencia is null) return;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await BloquearEmpresa(referencia.EmpresaId, ct);
        var sessao = await db.Sessoes.SingleAsync(x => x.Id == sessaoId, ct);
        if (sessao.RevogadaEm is null) await RevogarFamilia(sessao, "Logout", ct);
        await transacao.CommitAsync(ct);
    }

    private Task<bool> ContaValida(Sessao sessao, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == sessao.UsuarioId && x.EmpresaId == sessao.EmpresaId &&
            x.SecurityStamp == sessao.VersaoAcesso, ct);

    private async Task BloquearEmpresa(Guid empresaId, CancellationToken ct) =>
        _ = await db.Empresas.FromSqlInterpolated(
            $"SELECT * FROM Empresas WITH (UPDLOCK, HOLDLOCK) WHERE Id = {empresaId}").SingleAsync(ct);

    private async Task RevogarFamilia(Sessao sessao, string acao, CancellationToken ct)
    {
        sessao.RevogadaEm = relogio.Agora;
        await db.RefreshTokens.Where(x => x.FamiliaId == sessao.FamiliaRefreshId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.RevogadoEm, relogio.Agora), ct);
        Auditar(sessao.EmpresaId, sessao.UsuarioId, acao);
        await db.SaveChangesAsync(ct);
    }

    private ResultadoAutenticacao EmitirTokens(Sessao sessao)
    {
        var agora = relogio.Agora;
        var expiraEm = agora.Add(PoliticaSessao.AccessToken);
        if (expiraEm > sessao.ExpiraEm) expiraEm = sessao.ExpiraEm;
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        sessao.RefreshHash = Hash(refresh);
        db.RefreshTokens.Add(new RefreshTokenRegistro
        {
            SessaoId = sessao.Id, EmpresaId = sessao.EmpresaId, FamiliaId = sessao.FamiliaRefreshId,
            Hash = sessao.RefreshHash, CriadoEm = agora
        });
        var token = new JwtSecurityToken(jwt.Emissor, jwt.Audiencia,
            claims: [
                new(JwtRegisteredClaimNames.Sub, sessao.UsuarioId.ToString()),
                new("sid", sessao.Id.ToString()), new("empresaId", sessao.EmpresaId.ToString()),
                new("versaoAcesso", sessao.VersaoAcesso), new("jti", Guid.NewGuid().ToString()),
                new("iat", agora.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
            ],
            notBefore: agora.UtcDateTime, expires: expiraEm.UtcDateTime,
            signingCredentials: new SigningCredentials(jwt.Chaves[jwt.VersaoAtiva], SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), refresh, sessao.Id, expiraEm);
    }

    private void Auditar(Guid empresa, Guid usuario, string acao) =>
        db.Auditorias.Add(new Auditoria { EmpresaId = empresa, UsuarioId = usuario, Evento = acao, OcorridoEm = relogio.Agora });

    private static byte[] Hash(string valor) => SHA256.HashData(Encoding.UTF8.GetBytes(valor));
}
