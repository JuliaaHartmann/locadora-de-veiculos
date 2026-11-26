using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed partial class FotosTests
{
    [TestMethod]
    public async Task Foto_privada_condicional_e_uma_ativa_sem_herdar_da_placa()
    {
        using var local = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: local);
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        using var anonimo = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        _ = await EntrarEPreparar(b, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var foto = rota + "/foto";
        var etag = await Versao(a, rota);

        await Estado(await a.GetAsync(foto), HttpStatusCode.NotFound);
        await Estado(await anonimo.GetAsync(foto), HttpStatusCode.Unauthorized);
        await Estado(await EnviarFoto(anonimo, foto, etag), HttpStatusCode.Unauthorized);
        await Estado(await ExcluirFoto(anonimo, foto, etag), HttpStatusCode.Unauthorized);
        foreach (var x in new[] { await b.GetAsync(foto), await EnviarFoto(b, foto, etag), await ExcluirFoto(b, foto, etag) })
            await Estado(x, HttpStatusCode.NotFound);
        await Estado(await EnviarFoto(a, foto, null), HttpStatusCode.PreconditionRequired);
        await Estado(await EnviarFoto(a, foto, "\"vencido\""), HttpStatusCode.PreconditionFailed);
        var primeira = await EnviarFoto(a, foto, etag);
        await Estado(primeira, HttpStatusCode.NoContent);
        var versao = primeira.Headers.ETag!.ToString();
        Assert.AreNotEqual(etag, versao);
        var resposta = await a.GetAsync(foto);
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.AreEqual("image/png", resposta.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(resposta.Headers.CacheControl?.Private == true && resposta.Headers.CacheControl.NoStore);
        Assert.AreEqual("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.IsNull(resposta.Headers.Location);
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        Assert.IsNotEmpty(bytes);
        resposta.Dispose();

        var trocada = await EnviarFoto(a, foto, versao, Imagem(SKColors.Gold));
        await Estado(trocada, HttpStatusCode.NoContent);
        var versaoNova = trocada.Headers.ETag!.ToString();
        Assert.AreNotEqual(versao, versaoNova);
        await Estado(await ExcluirFoto(a, foto, versao), HttpStatusCode.PreconditionFailed);
        var nova = await a.GetByteArrayAsync(foto);
        Assert.IsFalse(bytes.SequenceEqual(nova));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = Banco(escopo, dados.Empresa);
        var fotos = await db.FotosObjetos.AsNoTracking().ToArrayAsync();
        Assert.AreEqual(1, fotos.Count(x => x.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(1, fotos.Count(x => x.Estado == EstadoFoto.Retida));
        Assert.IsTrue(fotos.All(x => x.UrlRelativa == foto && !x.Chave.Contains("nome-sintetico")));
        Assert.AreEqual(2, await db.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento.StartsWith("foto.")));

        var removida = await ExcluirFoto(a, foto, versaoNova);
        await Estado(removida, HttpStatusCode.NoContent);
        await Estado(await a.GetAsync(foto), HttpStatusCode.NotFound);
        var terceira = await EnviarFoto(a, foto, removida.Headers.ETag!.ToString());
        await Estado(terceira, HttpStatusCode.NoContent);
        await Estado(await Enviar(a, HttpMethod.Delete, rota, null, "If-Match", terceira.Headers.ETag!.ToString()), HttpStatusCode.NoContent);
        await Estado(await a.GetAsync(foto), HttpStatusCode.NotFound);
        var novo = await a.PostAsJsonAsync("/api/v1/veiculos", new
        {
            grupoId = dados.Grupo.Id, placa = dados.Veiculo.Placa, marca = "Marca", modelo = "Modelo",
            cor = "Cor", ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50",
        });
        await Estado(novo, HttpStatusCode.Created);
        var novoId = (await novo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.AreNotEqual(dados.Veiculo.Id, novoId);
        var novaFoto = $"/api/v1/veiculos/{novoId}/foto";
        await Estado(await a.GetAsync(novaFoto), HttpStatusCode.NotFound);
        await Estado(await a.PostAsJsonAsync("/api/v1/session/logout", new { }), HttpStatusCode.NoContent);
        await Estado(await a.GetAsync(novaFoto), HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Limita_multipart_com_e_sem_tamanho_e_documenta_o_contrato()
    {
        using var local = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: local);
        using var a = fabrica.CreateClient();
        var dados = await EntrarEPreparar(a, fabrica);
        var rota = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var etag = await Versao(a, rota);
        var foto = rota + "/foto";
        await Estado(await EnviarFoto(a, foto, etag, Imagem(SKColors.Red)[..^2]), HttpStatusCode.BadRequest);
        await Estado(await EnviarFoto(a, foto, etag, "outro formato"u8.ToArray()), HttpStatusCode.UnsupportedMediaType);
        await Estado(await EnviarFoto(a, foto, etag, new byte[LimitesFoto.Corpo]), HttpStatusCode.RequestEntityTooLarge);
        using (var envio = new HttpRequestMessage(HttpMethod.Put, foto))
        {
            envio.Headers.TryAddWithoutValidation("If-Match", etag);
            envio.Content = new MultipartSemComprimento();
            await Estado(await a.SendAsync(envio), HttpStatusCode.RequestEntityTooLarge);
        }
        await Estado(await a.GetAsync(foto), HttpStatusCode.NotFound);
        var doc = await a.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operacoes = doc.GetProperty("paths").GetProperty("/api/v1/veiculos/{id}/foto");
        Assert.IsTrue(operacoes.GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").TryGetProperty("image/png", out _));
        Assert.IsTrue(operacoes.GetProperty("put").GetProperty("requestBody").GetProperty("content")
            .GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties").TryGetProperty("arquivo", out _));
        Assert.IsTrue(operacoes.GetProperty("delete").GetProperty("responses").GetProperty("204")
            .GetProperty("headers").TryGetProperty("ETag", out _));
    }

    [TestMethod]
    public async Task Migration_F5_preserva_contrato_e_rejeita_referencia_entre_empresas()
    {
        using var fabrica = new SqlServerWebApplicationFactory(prepararSchema: false);
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        await using var glob = fabrica.Services.CreateAsyncScope();
        var db = glob.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var migrador = db.GetService<IMigrator>();
        try
        {
            await migrador.MigrateAsync();
            var dados = await EntrarEPreparar(a, fabrica);
            var outros = await EntrarEPreparar(b, fabrica);
            await Estado(await Enviar(a, HttpMethod.Post, "/api/v1/alugueis", ComandoAtual(dados),
                 "Idempotency-Key", "legado-f4"), HttpStatusCode.Created);
            await migrador.MigrateAsync("20261123184507_Fase4DevolucaoOperacaoLocal");
            await migrador.MigrateAsync();
            glob.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(dados.Empresa);
            Assert.AreEqual(dados.Veiculo.Id, (await db.Veiculos.SingleAsync()).Id);
            Assert.AreEqual(1, await db.ContratosSnapshot.CountAsync());
            Assert.AreEqual(0, await db.FotosObjetos.CountAsync());
            db.FotosObjetos.Add(new FotoObjeto
            {
                VeiculoId = outros.Veiculo.Id, EmpresaId = dados.Empresa, Deposito = "local-privado",
                Chave = $"{dados.Empresa:N}/{Guid.NewGuid():N}", UrlRelativa = "/foto",
                Mime = "image/png", Tamanho = 3, Hash = new string('A', 64), Estado = EstadoFoto.Pendente,
            });
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<string> Versao(HttpClient http, string rota)
    {
        using var resposta = await http.GetAsync(rota);
        await Estado(resposta, HttpStatusCode.OK);
        return resposta.Headers.ETag!.ToString();
    }

    private static Task<HttpResponseMessage> ExcluirFoto(HttpClient http, string rota, string etag) =>
        Enviar(http, HttpMethod.Delete, rota, null, "If-Match", etag);

    private static async Task<HttpResponseMessage> EnviarFoto(HttpClient http, string rota, string? etag, byte[]? bytes = null,
        string mime = "image/png")
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, rota);
        if (etag is not null) req.Headers.TryAddWithoutValidation("If-Match", etag);
        using var form = new MultipartFormDataContent();
        using var arquivo = new ByteArrayContent(bytes ?? Imagem(SKColors.CadetBlue));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(arquivo, "arquivo", mime == "image/jpeg" ? "nome-sintetico.jpg" : "nome-sintetico.png");
        req.Content = form;
        return await http.SendAsync(req);
    }

    private static byte[] Imagem(SKColor cor, SKEncodedImageFormat formato = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(16, 12);
        bitmap.Erase(cor);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(formato, 90);
        return dados.ToArray();
    }

    private static async Task Estado(HttpResponseMessage resposta, HttpStatusCode esperado) =>
        Assert.AreEqual(esperado, resposta.StatusCode, resposta.StatusCode == esperado ? "" : await resposta.Content.ReadAsStringAsync());

    private static LocadoraDbContext Banco(AsyncServiceScope escopo, Guid empresa)
    {
        escopo.ServiceProvider.GetRequiredService<IContextoEmpresa>().Definir(empresa);
        return escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
    }

    private sealed class MultipartSemComprimento : HttpContent
    {
        public MultipartSemComprimento() =>
            Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=TesteF5");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync("--TesteF5\r\nContent-Disposition: form-data; name=\"arquivo\"; filename=\"teste.png\"\r\nContent-Type: image/png\r\n\r\n"u8.ToArray());
            await stream.WriteAsync(new byte[LimitesFoto.Corpo]);
            await stream.WriteAsync("\r\n--TesteF5--\r\n"u8.ToArray());
        }
    }

    private sealed class VolumeTeste : IArmazenamentoFotos, IDisposable
    {
        private readonly string pasta = Directory.CreateTempSubdirectory("locadora-f5-sql-").FullName;
        private ArmazenamentoLocalFotos Local => new(pasta);
        public string Deposito => Local.Deposito;
        public bool FalharLeitura { get; set; }
        public bool FalharGravacao { get; set; }
        public bool FalharRemocao { get; set; }
        public int Remocoes;
        public Func<FotoObjeto, CancellationToken, Task>? AposGravar { get; set; }

        public async Task GravarAsync(FotoObjeto objeto, ArraySegment<byte> bytes, CancellationToken ct)
        {
            if (FalharGravacao) throw FalhaFotoException.Indisponivel();
            await Local.GravarAsync(objeto, bytes, ct);
            if (AposGravar is not null) await AposGravar(objeto, ct);
        }

        public Task<Stream?> LerAsync(FotoObjeto objeto, CancellationToken ct) =>
            FalharLeitura ? throw FalhaFotoException.Indisponivel() : Local.LerAsync(objeto, ct);

        public Task RemoverAsync(FotoObjeto objeto, CancellationToken ct)
        {
            Interlocked.Increment(ref Remocoes);
            return FalharRemocao ? throw FalhaFotoException.Indisponivel() : Local.RemoverAsync(objeto, ct);
        }
        public void Dispose() => Directory.Delete(pasta, recursive: true);
    }
}
