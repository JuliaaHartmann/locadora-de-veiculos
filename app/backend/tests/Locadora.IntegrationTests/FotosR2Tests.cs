using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkiaSharp;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class FotosTests
{
    [TestMethod]
    [TestCategory("R2Real")]
    public async Task R2_real_usa_User_Secrets_e_preserva_privacidade_consistencia_e_retomada()
    {
        if (Environment.GetEnvironmentVariable("LOCADORA_ENSAIO_R2") != "1")
            Assert.Inconclusive("Ensaio remoto opt-in: use scripts/Testar-Integracao.ps1 -R2 após autorizar o bucket de desenvolvimento.");

        var falhaSql = new FalhaAtivacaoR2();
        using var fabrica = new SqlServerWebApplicationFactory(interceptorFotos: falhaSql, ensaioR2: true);
        var config = fabrica.Services.GetRequiredService<IConfiguration>();
        Assert.IsTrue(fabrica.Services.GetRequiredService<IHostEnvironment>().IsDevelopment());
        const string bucket = "locadora-veiculos-dev";
        Assert.IsTrue(config["Fotos:Provedor"] == "R2" && config["Fotos:R2:Bucket"] == bucket,
            "O User Secrets de Development deve habilitar exclusivamente o bucket de teste.");
        var storage = fabrica.Services.GetRequiredService<IArmazenamentoFotos>();
        Assert.IsInstanceOfType<ArmazenamentoR2Fotos>(storage);

        using var a = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var b = fabrica.CreateClient();
        using var anonimo = fabrica.CreateClient();
        var dadosA = await EntrarEPreparar(a, fabrica);
        var dadosB = await EntrarEPreparar(b, fabrica);
        Assert.AreNotEqual(dadosA.Empresa, dadosB.Empresa);
        var veiculoA = $"/api/v1/veiculos/{dadosA.Veiculo.Id}";
        var fotoA = veiculoA + "/foto";
        var veiculoB = $"/api/v1/veiculos/{dadosB.Veiculo.Id}";
        var fotoB = veiculoB + "/foto";
        await using var escopoA = fabrica.Services.CreateAsyncScope();
        await using var escopoB = fabrica.Services.CreateAsyncScope();
        var bancoA = Banco(escopoA, dadosA.Empresa);
        var bancoB = Banco(escopoB, dadosB.Empresa);
        var tagA = await Versao(a, veiculoA);
        var tagB = await Versao(b, veiculoB);

        await R2Negar(anonimo, fotoA, tagA, HttpStatusCode.Unauthorized);
        await R2Negar(b, fotoA, tagA, HttpStatusCode.NotFound);
        await R2Negar(a, fotoB, tagB, HttpStatusCode.NotFound);
        R2Status(await a.GetAsync(fotoA), HttpStatusCode.NotFound, "Veículo sem foto.");
        tagA = R2NovaVersao(await EnviarFoto(a, fotoA, tagA), tagA, "Upload PNG da empresa A.");
        tagB = R2NovaVersao(await EnviarFoto(b, fotoB, tagB, Imagem(SKColors.Gold)), tagB, "Upload PNG da empresa B.");

        var original = await bancoA.FotosObjetos.AsNoTracking().SingleAsync(x => x.Estado == EstadoFoto.Ativa);
        var objetoB = await bancoB.FotosObjetos.AsNoTracking().SingleAsync(x => x.Estado == EstadoFoto.Ativa);
        Assert.IsTrue(original.Deposito == storage.Deposito && objetoB.Deposito == storage.Deposito);
        Assert.IsTrue(original.Chave.StartsWith($"{dadosA.Empresa:N}/", StringComparison.Ordinal));
        Assert.IsTrue(objetoB.Chave.StartsWith($"{dadosB.Empresa:N}/", StringComparison.Ordinal));
        await R2LerProxy(a, fotoA, original);
        await R2LerProxy(b, fotoB, objetoB);
        await R2Negar(b, fotoA, tagA, HttpStatusCode.NotFound);
        await R2Negar(a, fotoB, tagB, HttpStatusCode.NotFound);
        await R2Negar(anonimo, fotoA, tagA, HttpStatusCode.Unauthorized);
        // Ensaio da resposta a objeto inesperado, sem alterar ou apagar o objeto no R2.
        var atual = bancoA.FotosObjetos.Where(x => x.Id == original.Id);
        await atual.ExecuteUpdateAsync(x => x.SetProperty(y => y.Tamanho, original.Tamanho + 1));
        try
        {
            using var indisponivel = await a.GetAsync(fotoA);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, indisponivel.StatusCode);
            Assert.AreEqual("foto_indisponivel",
                (await indisponivel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            R2Status(await a.GetAsync(veiculoA), HttpStatusCode.OK, "Consulta cadastral independente de imagem.");
        }
        finally { await atual.ExecuteUpdateAsync(x => x.SetProperty(y => y.Tamanho, original.Tamanho)); }

        falhaSql.Ativa = true;
        R2Status(await EnviarFoto(a, fotoA, tagA, Imagem(SKColors.Red)), HttpStatusCode.InternalServerError,
            "Falha de SQL injetada antes da ativação, depois da escrita real.");
        falhaSql.Ativa = false;
        var pendente = await bancoA.FotosObjetos.AsNoTracking().SingleAsync(x => x.Estado == EstadoFoto.Pendente);
        Assert.AreEqual(tagA, await Versao(a, veiculoA));
        await R2LerProxy(a, fotoA, original);

        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using (var gravado = await storage.LerAsync(pendente, prazo.Token))
        {
            Assert.IsNotNull(gravado);
            Assert.AreEqual(pendente.Hash, Convert.ToHexString(await SHA256.HashDataAsync(gravado, prazo.Token)));
        }
        await bancoA.FotosObjetos.Where(x => x.Id == pendente.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.RetomarAposUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await escopoA.ServiceProvider.GetRequiredService<FotosService>().RetomarAsync(prazo.Token);
        Assert.AreEqual(EstadoFoto.Removida,
            await bancoA.FotosObjetos.Where(x => x.Id == pendente.Id).Select(x => x.Estado).SingleAsync());
        await storage.RemoverAsync(pendente, prazo.Token);
        await using var removido = await storage.LerAsync(pendente, prazo.Token);
        Assert.IsNull(removido);
        await R2LerProxy(a, fotoA, original);

        var jpeg = Imagem(SKColors.ForestGreen, SKEncodedImageFormat.Jpeg);
        tagA = R2NovaVersao(await EnviarFoto(a, fotoA, tagA, jpeg, "image/jpeg"), tagA, "Substituição por JPEG.");
        var substituta = await bancoA.FotosObjetos.AsNoTracking().SingleAsync(x => x.Estado == EstadoFoto.Ativa);
        Assert.IsTrue(substituta.Chave != original.Chave && substituta.Hash != original.Hash);
        Assert.AreEqual("image/jpeg", substituta.Mime);
        await R2LerProxy(a, fotoA, substituta);
        R2NovaVersao(await ExcluirFoto(a, fotoA, tagA), tagA, "Remoção lógica em A.");
        R2NovaVersao(await ExcluirFoto(b, fotoB, tagB), tagB, "Remoção lógica em B.");
        R2Status(await a.GetAsync(fotoA), HttpStatusCode.NotFound, "Foto removida em A.");
        R2Status(await b.GetAsync(fotoB), HttpStatusCode.NotFound, "Foto removida em B.");
        Assert.AreEqual(2, await bancoA.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Retida));
        Assert.AreEqual(1, await bancoB.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Retida));
        R2Status(await a.PostAsJsonAsync("/api/v1/session/logout", new { }), HttpStatusCode.NoContent, "Revogação.");
        await R2Negar(a, fotoA, tagA, HttpStatusCode.Unauthorized);
        await R2VerificarLeituraSemAssinatura(new Uri(config["Fotos:R2:Endpoint"]!, UriKind.Absolute), bucket, original.Chave);
    }

    private static void R2Status(HttpResponseMessage resposta, HttpStatusCode esperado, string etapa)
    {
        using (resposta)
            Assert.AreEqual(esperado, resposta.StatusCode, etapa);
    }

    private static string R2NovaVersao(HttpResponseMessage resposta, string anterior, string etapa)
    {
        using (resposta)
        {
            Assert.AreEqual(HttpStatusCode.NoContent, resposta.StatusCode, etapa);
            var atual = resposta.Headers.ETag;
            Assert.IsNotNull(atual);
            Assert.IsFalse(atual.IsWeak);
            Assert.AreNotEqual(anterior, atual.ToString());
            return atual.ToString();
        }
    }

    private static async Task R2Negar(HttpClient cliente, string rota, string tag, HttpStatusCode status)
    {
        R2Status(await cliente.GetAsync(rota), status, "GET sem autorização.");
        R2Status(await EnviarFoto(cliente, rota, tag), status, "PUT sem autorização.");
        R2Status(await ExcluirFoto(cliente, rota, tag), status, "DELETE sem autorização.");
    }

    private static async Task R2LerProxy(HttpClient cliente, string rota, FotoObjeto esperado)
    {
        using var resposta = await cliente.GetAsync(rota);
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode, "Leitura autenticada pelo proxy.");
        Assert.AreEqual(esperado.Mime, resposta.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(resposta.Headers.CacheControl?.Private == true && resposta.Headers.CacheControl.NoStore);
        Assert.AreEqual("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.IsNull(resposta.Headers.Location);
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        Assert.HasCount(checked((int)esperado.Tamanho), bytes);
        Assert.AreEqual(esperado.Hash, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static async Task R2VerificarLeituraSemAssinatura(Uri endpoint, string bucket, string chave)
    {
        using var cliente = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { Timeout = TimeSpan.FromSeconds(15) };
        using var resposta = await cliente.GetAsync(new Uri(endpoint, $"/{bucket}/{chave}"));
        Assert.IsNull(resposta.Headers.Location);
        var mediaType = resposta.Content.Headers.ContentType?.MediaType;
        Assert.IsFalse(mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);
        if (resposta.StatusCode == HttpStatusCode.Forbidden)
        {
            Console.WriteLine("Endpoint S3 sem assinatura: HTTP 403, sem imagem.");
            return;
        }
        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode, "GET S3 sem assinatura.");
        // O R2 pode responder 400 quando falta Authorization; não aceitar um 400 de outra causa.
        Assert.IsNotNull(mediaType);
        StringAssert.Contains(mediaType.ToLowerInvariant(), "xml");
        var erro = XDocument.Parse(await resposta.Content.ReadAsStringAsync()).Root;
        Assert.IsNotNull(erro);
        Assert.AreEqual("Error", erro.Name.LocalName);
        var codigo = erro.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value;
        var mensagem = erro.Elements().FirstOrDefault(x => x.Name.LocalName == "Message")?.Value ?? "";
        Assert.IsTrue(codigo is "AccessDenied" or "InvalidArgument" or "InvalidRequest" or
            "MissingAuthenticationToken" or "AuthorizationHeaderMalformed",
            "O R2 deve retornar um erro S3 compatível com a rejeição de uma requisição sem assinatura.");
        Assert.IsTrue(mensagem.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("signature", StringComparison.OrdinalIgnoreCase) ||
            mensagem.Contains("authenticate", StringComparison.OrdinalIgnoreCase),
            "A falha HTTP 400 deve se referir à autenticação/assinatura (mensagem não registrada).");
        Console.WriteLine("Endpoint S3 sem assinatura: HTTP 400 com erro de autenticação/assinatura, sem imagem.");
    }

    private sealed class FalhaAtivacaoR2 : SaveChangesInterceptor
    {
        public bool Ativa { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Ativa && eventData.Context!.ChangeTracker.Entries<Auditoria>()
                .Any(x => x.State == EntityState.Added && x.Entity.Evento == "foto.substituida"))
                throw new InvalidOperationException("Falha SQL sintética antes da auditoria.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
