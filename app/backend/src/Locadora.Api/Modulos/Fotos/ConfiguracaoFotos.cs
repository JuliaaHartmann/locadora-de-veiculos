using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;

namespace Locadora.Api;

internal static class ConfiguracaoFotos
{
    public static void AdicionarFotos(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IRepositorioFotos, RepositorioFotos>();
        builder.Services.AddScoped<FotosService>();
        builder.Services.AddSingleton<ISaneadorFoto, SaneadorFoto>();
        builder.Services.AddSingleton<LimiteProcessamentoFotos>();
        builder.Services.AddSingleton<IArmazenamentoFotos>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>().GetSection("Fotos");
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var local = env.IsDevelopment() || env.IsEnvironment("Testing");
            return (config["Provedor"] ?? (local ? "Local" : "Desativado")) switch
            {
                "Local" when local => new ArmazenamentoLocalFotos(RaizLocal(config, env)),
                "R2" => new ArmazenamentoR2Fotos(config["R2:Endpoint"] ?? "", config["R2:Bucket"] ?? "",
                    config["R2:AccessKeyId"] ?? "", config["R2:SecretAccessKey"] ?? ""),
                "Desativado" => new ArmazenamentoDesativado(),
                _ => throw new InvalidOperationException("Provedor privado de fotos inválido para o ambiente."),
            };
        });
    }

    private static string RaizLocal(IConfiguration config, IWebHostEnvironment env)
    {
        var caminho = config["PastaPrivada"] ?? (env.IsEnvironment("Testing")
            ? Path.Combine(Path.GetTempPath(), "locadora-fotos-teste", Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locadora", "fotos-privadas"));
        if (!Path.IsPathFullyQualified(caminho)) throw new InvalidOperationException("A pasta privada de fotos exige um caminho absoluto.");
        var raiz = Path.GetFullPath(caminho);
        var publica = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        if (Contem(publica, raiz) || Contem(raiz, publica) || Contem(env.ContentRootPath, raiz) || Contem(raiz, env.ContentRootPath))
            throw new InvalidOperationException("A pasta de fotos deve ficar fora das raízes da aplicação.");
        return raiz;
    }

    private static bool Contem(string pai, string caminho)
    {
        var relativo = Path.GetRelativePath(pai, caminho);
        return !Path.IsPathRooted(relativo) && relativo != ".." && !relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private sealed class ArmazenamentoDesativado : IArmazenamentoFotos
    {
        public string Deposito => "desativado";
        public Task GravarAsync(FotoObjeto objeto, ArraySegment<byte> bytes, CancellationToken ct) => throw FalhaFotoException.Indisponivel();
        public Task<Stream?> LerAsync(FotoObjeto objeto, CancellationToken ct) => throw FalhaFotoException.Indisponivel();
        public Task RemoverAsync(FotoObjeto objeto, CancellationToken ct) => throw FalhaFotoException.Indisponivel();
    }
}
