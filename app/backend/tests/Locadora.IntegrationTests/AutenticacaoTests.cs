using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class AutenticacaoTests
{
    [TestMethod]
    public async Task Registro_nao_exige_empresa_nao_abre_sessao_e_aceita_senha_sem_composicao()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = "administrador", email = "administrador@example.test", password = "12345678" });
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.IsFalse(corpo.Contains("token", StringComparison.OrdinalIgnoreCase));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        Assert.AreEqual(1, await banco.Empresas.CountAsync());
        Assert.AreEqual(1, await banco.Users.CountAsync());
        Assert.AreEqual(0, await banco.Sessoes.CountAsync());
        Assert.AreEqual(1, await banco.Auditorias.CountAsync());
    }

    [TestMethod]
    public async Task CA01_login_logout_idempotente_e_reuso_negado()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        Autorizar(cliente, tokens);
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/session")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await cliente.PostAsync("/api/v1/session/logout", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await cliente.PostAsync("/api/v1/session/logout", null)).StatusCode);
        await Problema(await cliente.GetAsync("/api/v1/session"), HttpStatusCode.Unauthorized);
        cliente.DefaultRequestHeaders.Authorization = null;
        await Problema(await Renovar(cliente, tokens), HttpStatusCode.Unauthorized);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        Assert.IsTrue(await banco.RefreshTokens.AllAsync(x => x.RevogadoEm != null));
        Assert.AreEqual(1, await banco.Auditorias.CountAsync(x => x.Evento == "Logout"));
    }

    [TestMethod]
    public async Task Duplicado_normalizado_e_falha_identity_nao_deixam_empresa_orfa()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        await Registrar(cliente, "unico");
        await Problema(await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = "UNICO", email = "outro@example.test", password = "12345678" }), HttpStatusCode.Conflict);
        await Problema(await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = "usuario invalido", email = "outro@example.test", password = "12345678" }), HttpStatusCode.BadRequest);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.AreEqual(1, await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Empresas.CountAsync());
    }

    [TestMethod]
    public async Task Falha_apos_inserir_conta_reverte_empresa_conta_e_auditoria()
    {
        using var fabrica = new SqlServerWebApplicationFactory(falharAuditoria: true);
        using var cliente = fabrica.CreateClient();
        await Problema(await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = "rollback", email = "sintetico@example.test", password = "12345678" }), HttpStatusCode.InternalServerError);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        Assert.AreEqual(0, await banco.Empresas.CountAsync());
        Assert.AreEqual(0, await banco.Users.CountAsync());
        Assert.AreEqual(0, await banco.Auditorias.CountAsync());
    }

    [TestMethod]
    public async Task Migration_sobre_schema_anterior_preserva_contas_e_revoga_sessoes_antigas()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var cliente = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var migrador = banco.GetService<IMigrator>();
        await migrador.MigrateAsync("20261120212522_RecursoTenantTeste");
        var empresa = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var sessao = Guid.NewGuid();
        var refresh = Guid.NewGuid();
        var familia = Guid.NewGuid();
        var agora = DateTimeOffset.UtcNow;
        var fim = agora.AddHours(8);
        var hash = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        await banco.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Empresas (Id, Nome, CriadaEm) VALUES ({empresa}, N'Sintética', {agora});
            INSERT INTO Usuarios (Id, EmpresaId, UserName, NormalizedUserName, EmailConfirmed,
                PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ({usuario}, {empresa}, N'migracao', N'MIGRACAO', 0, 0, 0, 1, 0);
            INSERT INTO Sessoes (Id, EmpresaId, UsuarioId, CriadaEm, ExpiraEm, FamiliaRefreshId, RefreshHash)
                VALUES ({sessao}, {empresa}, {usuario}, {agora}, {fim}, {familia}, {hash});
            INSERT INTO RefreshTokens (Id, SessaoId, FamiliaId, Hash, CriadoEm)
                VALUES ({refresh}, {sessao}, {familia}, {hash}, {agora});
            """);
        await migrador.MigrateAsync();
        Assert.AreEqual(1, await banco.Empresas.CountAsync());
        Assert.AreEqual(1, await banco.Users.CountAsync());
        Assert.IsNotNull((await banco.Sessoes.SingleAsync()).RevogadaEm);
        var registro = await banco.RefreshTokens.SingleAsync();
        Assert.AreEqual(empresa, registro.EmpresaId);
        Assert.IsNotNull(registro.RevogadoEm);
        await banco.Database.EnsureDeletedAsync();
    }

    [TestMethod]
    [DataRow("", "invalido", "curta")]
    [DataRow("valido", "", "12345678")]
    public async Task Campos_invalidos_retornam_problem_details(string usuario, string email, string senha)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        await Problema(await cliente.PostAsJsonAsync("/api/v1/registro", new { username = usuario, email, password = senha }), HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Refresh_roda_na_mesma_sessao_e_reuso_revoga_familia()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var primeiro = await Registrar(cliente);
        var resposta = await Renovar(cliente, primeiro);
        var segundo = (await resposta.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.AreEqual(primeiro.SessaoId, segundo.SessaoId);
        Assert.AreNotEqual(primeiro.RefreshToken, segundo.RefreshToken);
        await Problema(await Renovar(cliente, primeiro), HttpStatusCode.Unauthorized);
        Autorizar(cliente, segundo);
        await Problema(await cliente.GetAsync("/api/v1/session"), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Refresh_concorrente_em_conexoes_separadas_nao_emite_duas_renovacoes()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var clienteA = fabrica.CreateClient();
        using var clienteB = fabrica.CreateClient();
        var tokens = await Registrar(clienteA);
        var respostas = await Task.WhenAll(Renovar(clienteA, tokens), Renovar(clienteB, tokens));
        Assert.AreEqual(1, respostas.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, respostas.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));
        var renovado = (await respostas.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
        Autorizar(clienteA, renovado);
        await Problema(await clienteA.GetAsync("/api/v1/session"), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Refresh_nao_estende_oito_horas_e_access_termina_no_fim_da_sessao()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var sessao = await banco.Sessoes.SingleAsync();
        Assert.IsTrue(sessao.ExpiraEm - sessao.CriadaEm <= TimeSpan.FromHours(8).Add(TimeSpan.FromSeconds(1)));
        var prazo = DateTimeOffset.UtcNow.AddMinutes(1);
        await banco.Sessoes.ExecuteUpdateAsync(x => x.SetProperty(s => s.ExpiraEm, prazo));
        var renovado = (await (await Renovar(cliente, tokens)).Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
        Assert.IsTrue(renovado.AccessExpiraEm <= prazo);
        await banco.Sessoes.ExecuteUpdateAsync(x => x.SetProperty(s => s.ExpiraEm, DateTimeOffset.UtcNow.AddSeconds(-1)));
        await Problema(await Renovar(cliente, renovado), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Conta_e_versao_sao_reconsultadas_a_cada_operacao()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Users
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString()));
        Autorizar(cliente, tokens);
        await Problema(await cliente.GetAsync("/api/v1/session"), HttpStatusCode.Unauthorized);
        cliente.DefaultRequestHeaders.Authorization = null;
        await Problema(await Renovar(cliente, tokens), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Jwt_contem_claims_minimas_e_duracao_de_quinze_minutos()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        Assert.AreEqual("v1", jwt.Header.Kid);
        Assert.AreEqual("HS256", jwt.Header.Alg);
        foreach (var claim in new[] { "sub", "sid", "jti", "iat", "exp", "versaoAcesso", "empresaId" })
            Assert.IsTrue(jwt.Claims.Any(x => x.Type == claim), claim);
        Assert.IsTrue(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromMinutes(15));
    }

    [TestMethod]
    [DataRow("assinatura")]
    [DataRow("emissor")]
    [DataRow("audiencia")]
    [DataRow("expirado")]
    [DataRow("futuro")]
    [DataRow("kid-desconhecido")]
    [DataRow("kid-ausente")]
    [DataRow("none")]
    [DataRow("algoritmo")]
    [DataRow("empresa")]
    [DataRow("usuario")]
    [DataRow("versao")]
    public async Task Jwt_invalido_e_rejeitado(string caso)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", AlterarJwt(tokens.AccessToken, caso));
        await Problema(await cliente.GetAsync("/api/v1/session"), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Chave_anterior_autorizada_valida_token_sem_assinar_tokens_novos()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var tokens = await Registrar(cliente);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", AlterarJwt(tokens.AccessToken, "anterior"));
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/session")).StatusCode);
        Assert.AreEqual("v1", new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Header.Kid);
    }

    [TestMethod]
    public async Task Bloqueio_apos_cinco_falhas_dura_quinze_minutos_e_expira()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        await Registrar(cliente);
        for (var i = 0; i < 5; i++)
        {
            await using var tentativa = fabrica.Services.CreateAsyncScope();
            Assert.IsNull(await tentativa.ServiceProvider.GetRequiredService<IAutenticacao>().LoginAsync("administrador", "errada", default));
        }
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var conta = await banco.Users.AsNoTracking().SingleAsync();
        Assert.IsTrue(conta.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14));
        Assert.IsNull(await escopo.ServiceProvider.GetRequiredService<IAutenticacao>().LoginAsync("administrador", "12345678", default));
        await banco.Users.ExecuteUpdateAsync(x => x.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1)));
        await using var desbloqueado = fabrica.Services.CreateAsyncScope();
        Assert.IsNotNull(await desbloqueado.ServiceProvider.GetRequiredService<IAutenticacao>().LoginAsync("administrador", "12345678", default));
    }

    [TestMethod]
    [DataRow("registro", 3)]
    [DataRow("login", 5)]
    [DataRow("refresh", 30)]
    public async Task Limites_independentes_retornam_429_sem_gravar_excedente(string endpoint, int limite)
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var rota = endpoint == "registro" ? "/api/v1/registro" : $"/api/v1/session/{endpoint}";
        for (var i = 0; i < limite; i++)
        {
            var resposta = await cliente.PostAsJsonAsync(rota,
                new { username = $"usuario{i}", email = "sintetico@example.test", password = "12345678", refreshToken = "invalido" });
            Assert.AreNotEqual(HttpStatusCode.TooManyRequests, resposta.StatusCode);
        }
        var excedente = await cliente.PostAsJsonAsync(rota,
            new { username = "excedente", email = "sintetico@example.test", password = "12345678", refreshToken = "invalido" });
        await Problema(excedente, HttpStatusCode.TooManyRequests);
        Assert.IsTrue(excedente.Headers.Contains("Retry-After"));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.AreEqual(endpoint == "registro" ? 3 : 0,
            await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Empresas.CountAsync());
    }

    [TestMethod]
    public async Task Cors_exato_aceita_preflight_e_inclui_origem_em_429()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        foreach (var origem in new[] { "http://localhost:4200", "http://preview.example.test" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/registro");
            pedido.Headers.Add("Origin", origem);
            pedido.Headers.Add("Access-Control-Request-Method", "POST");
            var resposta = await cliente.SendAsync(pedido);
            Assert.AreEqual(origem == "http://localhost:4200", resposta.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
        }
        cliente.DefaultRequestHeaders.Add("Origin", "http://localhost:4200");
        for (var i = 0; i < 4; i++) await cliente.PostAsJsonAsync("/api/v1/registro", new { });
        var limitado = await cliente.PostAsJsonAsync("/api/v1/registro", new { });
        Assert.AreEqual(HttpStatusCode.TooManyRequests, limitado.StatusCode);
        Assert.IsTrue(limitado.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [TestMethod]
    public async Task Empresas_AB_nao_cruzam_leitura_escrita_referencias_ou_historico()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        var a = await Registrar(cliente, "empresaA");
        var b = await Registrar(cliente, "empresaB");
        Autorizar(cliente, a);
        var recursoA = await CriarRecurso(cliente);
        Autorizar(cliente, b);
        await Problema(await cliente.GetAsync($"/api/v1/teste/tenant/{recursoA}"), HttpStatusCode.NotFound);
        await Problema(await cliente.PostAsJsonAsync("/api/v1/teste/tenant", new { valor = "cruzado", referenciaId = recursoA }), HttpStatusCode.NotFound);
        var recursoB = await CriarRecurso(cliente);
        Autorizar(cliente, a);
        cliente.DefaultRequestHeaders.Add("X-Empresa-Id", b.SessaoId.ToString());
        await Problema(await cliente.GetAsync($"/api/v1/teste/tenant/{recursoB}?empresaId={b.SessaoId}"), HttpStatusCode.NotFound);
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync($"/api/v1/teste/tenant/{recursoA}")).StatusCode);
        var historico = await cliente.GetFromJsonAsync<JsonElement[]>("/api/v1/teste/tenant/historico");
        Assert.IsNotNull(historico);
        Assert.IsNotEmpty(historico);
        Assert.AreEqual(1, historico.Select(x => x.GetProperty("empresaId").GetGuid()).Distinct().Count());
        await cliente.PostAsync("/api/v1/session/logout", null);
        await Problema(await cliente.GetAsync($"/api/v1/teste/tenant/{recursoA}"), HttpStatusCode.Unauthorized);
        await Problema(await cliente.GetAsync("/api/v1/teste/tenant/historico"), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Guarda_de_escrita_e_fk_composta_bloqueiam_attach_e_referencia_cruzada()
    {
        using var fabrica = new SqlServerWebApplicationFactory();
        using var cliente = fabrica.CreateClient();
        await Registrar(cliente, "empresaA");
        await Registrar(cliente, "empresaB");
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var empresas = await banco.Empresas.Select(x => x.Id).ToArrayAsync();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresas[0]);
        banco.RecursosTenantTeste.Add(new() { EmpresaId = empresas[1], Valor = "proibido" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => banco.SaveChangesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => banco.SaveChangesAsync(acceptAllChangesOnSuccess: true));
        Assert.Throws<UnauthorizedAccessException>(() => banco.SaveChanges());
        banco.ChangeTracker.Clear();
        var recurso = new RecursoTenantTeste { EmpresaId = empresas[0], Valor = "original" };
        banco.Add(recurso);
        await banco.SaveChangesAsync();
        banco.ChangeTracker.Clear();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresas[1]);
        banco.Add(new RecursoTenantTeste { EmpresaId = empresas[1], Valor = "cruzado", ReferenciaId = recurso.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => banco.SaveChangesAsync());
        banco.ChangeTracker.Clear();
        var recursoB = new RecursoTenantTeste { EmpresaId = empresas[1], Valor = "original B" };
        banco.Add(recursoB);
        await banco.SaveChangesAsync();
        banco.ChangeTracker.Clear();
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresas[0]);
        var forjado = new RecursoTenantTeste { Id = recursoB.Id, EmpresaId = empresas[0], Valor = "ataque attach" };
        banco.Attach(forjado);
        banco.Entry(forjado).Property(x => x.Valor).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => banco.SaveChangesAsync());
    }

    [TestMethod]
    [DataRow("/api/v1/session")]
    [DataRow("/api/v1/session/login")]
    [DataRow("/api/v1/session/refresh")]
    [DataRow("/api/v1/registro")]
    public async Task Sql_indisponivel_falha_fechado_com_503(string rota)
    {
        using var fabrica = new SqlServerWebApplicationFactory(indisponivel: true);
        using var cliente = fabrica.CreateClient();
        HttpResponseMessage resposta;
        if (rota == "/api/v1/session")
        {
            var claims = new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim("sid", Guid.NewGuid().ToString()),
                new Claim("empresaId", Guid.NewGuid().ToString()), new Claim("versaoAcesso", "teste") };
            cliente.DefaultRequestHeaders.Authorization = new("Bearer", CriarJwt(claims));
            resposta = await cliente.GetAsync(rota);
        }
        else resposta = await cliente.PostAsJsonAsync(rota,
            new { username = "sintetico", email = "sintetico@example.test", password = "12345678", refreshToken = "invalido" });
        await Problema(resposta, HttpStatusCode.ServiceUnavailable);
    }

    [TestMethod]
    public async Task Ambiente_nao_development_nao_cria_schema_automaticamente()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var cliente = fabrica.CreateClient();
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.IsFalse(await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.CanConnectAsync());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("invalida")]
    [DataRow("YQ==")]
    public void Configuracao_rejeita_chave_ausente_invalida_ou_tamanho_incorreto(string? chave)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacao:Jwt:Emissor"] = "locadora-api", ["Autenticacao:Jwt:Audiencia"] = "locadora-web",
            ["Autenticacao:Jwt:VersaoAtiva"] = "v1", ["Autenticacao:Jwt:Chaves:v1"] = chave
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ConfiguracaoJwt(config));
    }

    private static async Task<ResultadoAutenticacao> Registrar(HttpClient cliente, string usuario = "administrador")
    {
        var registro = await cliente.PostAsJsonAsync("/api/v1/registro",
            new { username = usuario, email = "sintetico@example.test", password = "12345678" });
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        var login = await cliente.PostAsJsonAsync("/api/v1/session/login", new { username = usuario, password = "12345678" });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        Assert.AreEqual("no-store", login.Headers.CacheControl?.ToString());
        return (await login.Content.ReadFromJsonAsync<ResultadoAutenticacao>())!;
    }

    private static async Task<Guid> CriarRecurso(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/teste/tenant", new { valor = "sintetico", empresaId = Guid.NewGuid() });
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static void Autorizar(HttpClient cliente, ResultadoAutenticacao tokens) =>
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
    private static Task<HttpResponseMessage> Renovar(HttpClient cliente, ResultadoAutenticacao tokens) =>
        cliente.PostAsJsonAsync("/api/v1/session/refresh", new { refreshToken = tokens.RefreshToken });

    private static async Task Problema(HttpResponseMessage resposta, HttpStatusCode status)
    {
        Assert.AreEqual(status, resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual((int)status, json.GetProperty("status").GetInt32());
        Assert.IsTrue(json.TryGetProperty("code", out _));
        Assert.IsTrue(json.TryGetProperty("traceId", out _));
        var texto = json.ToString();
        foreach (var proibido in new[] { "12345678", "accessToken", "SqlException", "stackTrace", "Password=" })
            Assert.IsFalse(texto.Contains(proibido, StringComparison.OrdinalIgnoreCase));
    }

    private static string AlterarJwt(string original, string caso)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(original);
        var claims = jwt.Claims.Where(x => x.Type is not ("iss" or "aud" or "exp" or "nbf" or "iat")).ToList();
        var tipo = caso switch { "empresa" => "empresaId", "usuario" => "sub", "versao" => "versaoAcesso", _ => "" };
        if (tipo != "") { claims.RemoveAll(x => x.Type == tipo); claims.Add(new(tipo, Guid.NewGuid().ToString())); }
        return CriarJwt(claims, caso);
    }

    private static string CriarJwt(IEnumerable<Claim> claims, string caso = "")
    {
        var chave = Convert.FromBase64String(Environment.GetEnvironmentVariable(
            caso == "anterior" ? "Autenticacao__Jwt__Chaves__anterior" : "Autenticacao__Jwt__Chaves__v1")!);
        if (caso == "assinatura") chave = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        if (caso == "algoritmo") chave = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        var credencial = caso == "none" ? null : new SigningCredentials(
            new SymmetricSecurityKey(chave) { KeyId = caso == "anterior" ? "anterior" : caso == "kid-desconhecido" ? "desconhecido" : "v1" },
            caso == "algoritmo" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(caso == "emissor" ? "invalido" : "locadora-api",
            caso == "audiencia" ? "invalida" : "locadora-web", claims,
            DateTime.UtcNow.AddMinutes(caso == "futuro" ? 5 : -20),
            DateTime.UtcNow.AddMinutes(caso == "expirado" ? -1 : 15), credencial);
        if (caso == "kid-ausente") token.Header.Remove("kid");
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class SqlServerWebApplicationFactory(bool indisponivel = false, bool prepararSchema = true, bool falharAuditoria = false)
    : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"LocadoraTeste_{Guid.NewGuid():N}";
    private bool descartado;
    private IHost? iniciado;
    private bool schemaPreparado;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var original = Environment.GetEnvironmentVariable("ConnectionStrings__Locadora")
                ?? throw new InvalidOperationException("Execute scripts/Testar-Integracao.ps1 ou forneça SQL exclusivo de testes.");
            var sql = new SqlConnectionStringBuilder(original) { InitialCatalog = databaseName };
            if (indisponivel) { sql.DataSource = "127.0.0.1,1"; sql.ConnectTimeout = 1; }
            services.RemoveAll<DbContextOptions<LocadoraDbContext>>();
            services.AddDbContext<LocadoraDbContext>(options =>
            {
                options.UseSqlServer(sql.ConnectionString);
                if (falharAuditoria) options.AddInterceptors(new FalhaAuditoriaInterceptor());
            });
            services.AddControllers().AddApplicationPart(typeof(RecursoTenantTesteController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        iniciado = host;
        if (!indisponivel && prepararSchema)
        {
            using var escopo = host.Services.CreateScope();
            escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.Migrate();
            schemaPreparado = true;
        }
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (descartado) return;
        descartado = true;
        if (disposing && schemaPreparado && iniciado is not null)
        {
            using var escopo = iniciado.Services.CreateScope();
            escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}

internal sealed class FalhaAuditoriaInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Auditoria>().Any(x => x.State == EntityState.Added))
            throw new InvalidOperationException("Falha sintética antes da auditoria.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
