using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.Infrastructure;

public sealed class ConfiguracaoJwt
{
    public string Emissor { get; }
    public string Audiencia { get; }
    public string VersaoAtiva { get; }
    public IReadOnlyDictionary<string, SecurityKey> Chaves { get; }

    public ConfiguracaoJwt(IConfiguration configuracao)
    {
        var secao = configuracao.GetSection("Autenticacao:Jwt");
        Emissor = Obrigatoria(secao["Emissor"]);
        Audiencia = Obrigatoria(secao["Audiencia"]);
        VersaoAtiva = Obrigatoria(secao["VersaoAtiva"]);
        var chaves = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);
        foreach (var entrada in secao.GetSection("Chaves").GetChildren())
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(entrada.Value ?? ""); }
            catch (FormatException) { throw new InvalidOperationException("Configuração JWT inválida."); }
            if (bytes.Length != 32) throw new InvalidOperationException("Chaves JWT devem possuir 32 bytes.");
            chaves.Add(entrada.Key, new SymmetricSecurityKey(bytes) { KeyId = entrada.Key });
        }
        if (!chaves.ContainsKey(VersaoAtiva)) throw new InvalidOperationException("Versão ativa JWT ausente do mapa.");
        Chaves = chaves;
    }

    private static string Obrigatoria(string? valor) => string.IsNullOrWhiteSpace(valor)
        ? throw new InvalidOperationException("Configuração JWT incompleta.") : valor;
}
