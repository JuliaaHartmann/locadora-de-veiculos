using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;

namespace Locadora.UnitTests;

[TestClass]
public sealed class ArmazenamentoLocalFotosTests
{
    [TestMethod]
    public async Task Persiste_em_pasta_privada_valida_tamanho_e_remove_de_forma_idempotente()
    {
        using var pasta = DiretoriaTeste.Criar();
        var loja = new ArmazenamentoLocalFotos(pasta.Caminho);
        var empresa = Guid.NewGuid();
        var chave = $"{empresa:N}/{Guid.NewGuid():N}";
        var objeto = Montar(empresa, chave);
        Assert.IsNull(await loja.LerAsync(objeto, default));
        await loja.GravarAsync(objeto, "abc"u8.ToArray(), default);
        await loja.GravarAsync(objeto, "def"u8.ToArray(), default);
        await using (var stream = await loja.LerAsync(objeto, default))
        {
            using var leitor = new StreamReader(stream!);
            Assert.AreEqual("abc", await leitor.ReadToEndAsync());
        }
        var fisico = Path.Combine(pasta.Caminho, chave.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllTextAsync(fisico, "alterado");
        Assert.AreEqual(TipoFalhaFoto.Indisponivel, (await Assert.ThrowsExactlyAsync<FalhaFotoException>(() => loja.LerAsync(objeto, default))).Tipo);
        await Assert.ThrowsExactlyAsync<FalhaFotoException>(() => loja.LerAsync(Montar(empresa, $"../{chave}"), default));
        await loja.RemoverAsync(objeto, default);
        await loja.RemoverAsync(objeto, default);
        Assert.IsNull(await loja.LerAsync(objeto, default));
    }

    private static FotoObjeto Montar(Guid empresa, string chave) => new()
    {
        EmpresaId = empresa, Deposito = "local-privado", Chave = chave, Mime = "image/png", Tamanho = 3, UrlRelativa = "/foto", Hash = "",
    };

    private sealed class DiretoriaTeste(string caminho) : IDisposable
    {
        public string Caminho { get; } = caminho;
        public static DiretoriaTeste Criar() => new(Directory.CreateTempSubdirectory("locadora-f5-teste-").FullName);
        public void Dispose() => Directory.Delete(Caminho, recursive: true);
    }
}
