using System.Globalization;
using System.Threading.RateLimiting;
using Locadora.Application;
using Locadora.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// CreateBuilder já aplica User Secrets somente em Development e respeita a precedência do ambiente.
var connection = builder.Configuration.GetConnectionString("Locadora");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Conexão não configurada.");
var jwt = new ConfiguracaoJwt(builder.Configuration);
builder.Services.AddSingleton(jwt);
builder.Services.AddDbContext<LocadoraDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddIdentityCore<UsuarioIdentity>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
}).AddEntityFrameworkStores<LocadoraDbContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRelogio, Relogio>();
builder.Services.AddScoped<IContextoEmpresa, ContextoEmpresa>();
builder.Services.AddScoped<IAutenticacao, AutenticacaoService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = jwt.Emissor,
        ValidateAudience = true, ValidAudience = jwt.Audiencia,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKeyResolver = (_, _, kid, _) =>
            kid is not null && jwt.Chaves.TryGetValue(kid, out var chave) ? [chave] : [],
        ValidateLifetime = true, ClockSkew = PoliticaSessao.ToleranciaRelogio,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var usuario = context.Principal!;
            if (!Guid.TryParse(usuario.FindFirst("sid")?.Value, out var sid) ||
                !Guid.TryParse(usuario.FindFirst("sub")?.Value, out var sub) ||
                !Guid.TryParse(usuario.FindFirst("empresaId")?.Value, out var empresa))
            { context.Fail("Sessão inválida."); return; }
            var autenticacao = context.HttpContext.RequestServices.GetRequiredService<IAutenticacao>();
            var logout = context.Request.Path == "/api/v1/session/logout" && HttpMethods.IsPost(context.Request.Method);
            var sessao = await autenticacao.ValidarSessaoAsync(sid, sub, empresa,
                usuario.FindFirst("versaoAcesso")?.Value ?? "", logout, context.HttpContext.RequestAborted);
            if (sessao is null) { context.Fail("Sessão inválida."); return; }
            context.HttpContext.RequestServices.GetRequiredService<IContextoEmpresa>().Definir(sessao.EmpresaId);
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await ErrosHttp.Escrever(context.HttpContext, 401);
        },
        OnForbidden = context => ErrosHttp.Escrever(context.HttpContext, 403)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        var espera = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry : TimeSpan.FromMinutes(10);
        context.HttpContext.Response.Headers.RetryAfter =
            Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        await ErrosHttp.Escrever(context.HttpContext, 429);
    };
    foreach (var (nome, limite, minutos) in new[] { ("registro", 3, 10), ("login", 5, 1), ("refresh", 30, 1) })
    {
        var prefixo = char.ToUpperInvariant(nome[0]) + nome[1..];
        var permitido = builder.Configuration.GetValue($"RateLimit:{prefixo}Permitido", limite);
        var janela = builder.Configuration.GetValue($"RateLimit:{prefixo}JanelaMinutos", minutos);
        if (permitido <= 0 || janela <= 0) throw new InvalidOperationException("Rate limit inválido.");
        options.AddPolicy(nome, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            { PermitLimit = permitido, Window = TimeSpan.FromMinutes(janela), QueueLimit = 0 }));
    }
});
var origens = builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? [];
if (origens.Length == 0 || origens.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) ||
    (uri.Scheme != "http" && uri.Scheme != "https") || uri.GetLeftPart(UriPartial.Authority) != x))
    throw new InvalidOperationException("Origens CORS devem ser exatas.");
builder.Services.AddCors(options => options.AddPolicy("origens-configuradas",
    policy => policy.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    ErrosHttp.Completar(context.ProblemDetails, context.HttpContext));
builder.Services.AddControllers(options => options.ModelMetadataDetailsProviders
    .Add(new Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.SystemTextJsonValidationMetadataProvider()));
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await using var escopo = app.Services.CreateAsyncScope();
    await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.MigrateAsync();
    app.MapOpenApi();
}
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var erro = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var sql = erro is SqlException || erro?.InnerException is SqlException;
    await ErrosHttp.Escrever(context, sql ? 503 : 500);
}));
app.UseStatusCodePages(async context => await ErrosHttp.Escrever(context.HttpContext, context.HttpContext.Response.StatusCode));
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseRouting();
app.UseCors("origens-configuradas");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.Run();

public partial class Program { }

internal static class ErrosHttp
{
    public static void Completar(ProblemDetails problema, HttpContext context)
    {
        problema.Extensions["traceId"] = context.TraceIdentifier;
        problema.Extensions["code"] = Codigo(problema.Status ?? context.Response.StatusCode);
    }

    public static Task Escrever(HttpContext context, int status)
    {
        context.Response.Headers.CacheControl = "no-store";
        var problema = new ProblemDetails { Status = status, Title = status switch
        {
            401 => "Sessão inválida", 403 => "Acesso negado", 404 => "Recurso não encontrado",
            429 => "Limite de requisições excedido", 503 => "Serviço indisponível", _ => "Erro na requisição"
        }};
        Completar(problema, context);
        return Results.Problem(problema).ExecuteAsync(context);
    }

    private static string Codigo(int status) => status switch
    {
        400 => "validacao", 401 => "nao_autenticado", 403 => "acesso_negado",
        404 => "nao_encontrado", 409 => "conflito", 429 => "limite_excedido",
        503 => "dependencia_indisponivel", _ => "erro_requisicao"
    };
}
