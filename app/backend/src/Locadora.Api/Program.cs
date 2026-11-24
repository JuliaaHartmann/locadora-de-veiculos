using System.Globalization;
using System.Threading.RateLimiting;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// Erros de índice do SQL podem incluir o CPF/CNPJ duplicado no texto da exceção.
// A API mantém ProblemDetails/correlação, sem registrar o detalhe bruto do SaveChanges.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
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
builder.Services.AddScoped<IProtocoloEscrita, ProtocoloEscrita>();
builder.Services.AddScoped<IGrupoService, GrupoService>();
builder.Services.AddScoped<IPlanoService, PlanoService>();
builder.Services.AddScoped<IVeiculoService, VeiculoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICondutorService, CondutorService>();
builder.Services.AddScoped<ITaxaService, TaxaService>();
builder.Services.AddScoped<ISeguroService, SeguroService>();
builder.Services.AddScoped<ICombustivelService, CombustivelService>();
builder.Services.AddScoped<IRepositorioAlugueis, RepositorioAlugueis>();
builder.Services.AddScoped<AluguelService>();
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
            context.HttpContext.RequestServices.GetRequiredService<IContextoEmpresa>()
                .Definir(sessao.EmpresaId, sub);
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
    policy => policy.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("ETag")));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    ErrosHttp.Completar(context.ProblemDetails, context.HttpContext));
builder.Services.AddControllers(options => options.ModelMetadataDetailsProviders
        .Add(new Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters
        .Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    // Erros de binding/contrato seguem o mesmo ProblemDetails em português usado pelos serviços.
    options.InvalidModelStateResponseFactory = context =>
    {
        var erros = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .GroupBy(x => ErrosHttp.Campo(x.Key))
            .ToDictionary(x => x.Key,
                x => x.SelectMany(y => y.Value!.Errors).Select(_ => "Informe um valor válido.").ToArray());
        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dados inválidos",
            Detail = "Revise os dados informados.",
        };
        problema.Extensions["errors"] = erros;
        ErrosHttp.Completar(problema, context.HttpContext);
        return new ObjectResult(problema)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    };
});
builder.Services.AddOpenApi(Locadora.Api.ContratosOpenApi.Configurar);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await using var escopo = app.Services.CreateAsyncScope();
    await escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>().Database.MigrateAsync();
}
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    app.MapOpenApi();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var erro = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    switch (erro)
    {
        case ValidacaoException validacao:
            await ErrosHttp.Escrever(context, 400, "Dados inválidos", validacao.Erros
                .GroupBy(x => x.Campo)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Mensagem).ToArray()));
            return;
        case NaoEncontradoException:
            await ErrosHttp.Escrever(context, 404);
            return;
        case ConflitoException conflito:
            await ErrosHttp.Escrever(context, 409, "Conflito", conflito.Campo is null
                ? null
                : new Dictionary<string, string[]> { [conflito.Campo] = [conflito.Message] }, conflito.Message);
            return;
        case PrecondicaoObrigatoriaException precondicao:
            await ErrosHttp.Escrever(context, 428, "Precondição obrigatória", null, precondicao.Message);
            return;
        case PrecondicaoException precondicao:
            await ErrosHttp.Escrever(context, 412, "Precondição falhou", null, precondicao.Message);
            return;
        case FalhaGravacaoDevolucaoException falhaDevolucao:
            await ErrosHttp.Escrever(context, 503, "Devolução não gravada", detalhe: falhaDevolucao.Message,
                codigo: "devolucao_nao_gravada");
            return;
        case DbUpdateConcurrencyException:
            await ErrosHttp.Escrever(context, 412, "Precondição falhou",
                null, "O cadastro foi alterado por outra operação. Recarregue e tente novamente.");
            return;
        case UnauthorizedAccessException:
            await ErrosHttp.Escrever(context, 403);
            return;
        case SqlException sql when sql.Number is 2601 or 2627:
            await ErrosHttp.Escrever(context, 409, "Conflito", null, "O registro entra em conflito com um cadastro existente.");
            return;
        case DbUpdateException atualizacao when atualizacao.InnerException is SqlException sql && sql.Number is 2601 or 2627:
            await ErrosHttp.Escrever(context, 409, "Conflito", null, "O registro entra em conflito com um cadastro existente.");
            return;
        case DbUpdateException atualizacao when atualizacao.InnerException is SqlException:
            await ErrosHttp.Escrever(context, 503);
            return;
        case SqlException:
            await ErrosHttp.Escrever(context, 503);
            return;
        default:
            await ErrosHttp.Escrever(context, 500);
            return;
    }
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
    public static string Campo(string campo)
    {
        if (string.IsNullOrWhiteSpace(campo) || campo == "$") return "requisicao";
        if (campo.StartsWith("$.", StringComparison.Ordinal)) campo = campo[2..];
        if (campo.StartsWith("requisicao.", StringComparison.OrdinalIgnoreCase)) campo = campo[11..];
        return string.Join('.', campo.Split('.').Select(System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName));
    }

    public static void Completar(ProblemDetails problema, HttpContext context)
    {
        problema.Extensions["traceId"] = context.TraceIdentifier;
        problema.Extensions["code"] = Codigo(problema.Status ?? context.Response.StatusCode);
    }

    public static Task Escrever(HttpContext context, int status, string? titulo = null,
        IReadOnlyDictionary<string, string[]>? erros = null, string? detalhe = null, string? codigo = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        var problema = new ProblemDetails { Status = status, Title = titulo ?? status switch
        {
            401 => "Sessão inválida", 403 => "Acesso negado", 404 => "Recurso não encontrado",
            412 => "Precondição falhou", 428 => "Precondição obrigatória",
            429 => "Limite de requisições excedido", 503 => "Serviço indisponível",
            _ => "Erro na requisição"
        }, Detail = detalhe };
        if (erros is not null) problema.Extensions["errors"] = erros;
        Completar(problema, context);
        if (codigo is not null) problema.Extensions["code"] = codigo;
        return Results.Problem(problema).ExecuteAsync(context);
    }

    private static string Codigo(int status) => status switch
    {
        400 => "validacao", 401 => "nao_autenticado", 403 => "acesso_negado",
        404 => "nao_encontrado", 409 => "conflito", 412 => "precondicao_falhou",
        428 => "precondicao_obrigatoria", 429 => "limite_excedido",
        503 => "dependencia_indisponivel", _ => "erro_requisicao"
    };
}
