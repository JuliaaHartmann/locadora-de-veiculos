using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using Locadora.Testes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using static Locadora.IntegrationTests.ContratosLocacoesTests;

namespace Locadora.IntegrationTests;

public sealed partial class FotosTests
{
    [TestMethod]
    [DoNotParallelize]
    public async Task Limites_F5_rejeitam_antes_de_ativar_e_preservam_foto_existente()
    {
        using var volume = new VolumeTeste();
        var gravacoes = 0;
        volume.AposGravar = (_, _) => { gravacoes++; return Task.CompletedTask; };
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: volume);
        using var cliente = fabrica.CreateClient();
        var dados = await EntrarEPreparar(cliente, fabrica);
        var veiculo = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var rota = veiculo + "/foto";
        var etagInicial = await Versao(cliente, veiculo);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var banco = Banco(escopo, dados.Empresa);
        var png = AmostrasFotoLimites.PngSimples();
        var corrompido = (byte[])png.Clone();
        corrompido[30] ^= 1;
        var animado = AmostrasFotoLimites.InserirChunkPng(png, "acTL"u8, new byte[] { 0, 0, 0, 2, 0, 0, 0, 0 });
        using var acimaDosPixels = new SKBitmap(4001, 4000);
        acimaDosPixels.Erase(SKColors.Orange);
        using var pngAcima = acimaDosPixels.Encode(SKEncodedImageFormat.Png, 90);
        using var jpegAcima = acimaDosPixels.Encode(SKEncodedImageFormat.Jpeg, 90);
        var jpegTruncado = Imagem(SKColors.Blue, SKEncodedImageFormat.Jpeg)[..^2];

        foreach (var (bytes, mime, esperado) in new[]
        {
            (Array.Empty<byte>(), "image/png", HttpStatusCode.BadRequest),
            (corrompido, "image/png", HttpStatusCode.BadRequest),
            (animado, "image/png", HttpStatusCode.BadRequest),
            (jpegTruncado, "image/jpeg", HttpStatusCode.BadRequest),
            (pngAcima.ToArray(), "image/png", HttpStatusCode.BadRequest),
            (jpegAcima.ToArray(), "image/jpeg", HttpStatusCode.BadRequest),
            (png, "image/jpeg", HttpStatusCode.UnsupportedMediaType),
            ("GIF89a"u8.ToArray(), "image/png", HttpStatusCode.UnsupportedMediaType),
        })
        {
            using var resposta = await EnviarFoto(cliente, rota, etagInicial, bytes, mime);
            await Estado(resposta, esperado);
        }
        foreach (var informarComprimento in new[] { true, false })
        {
            using var acima = await EnviarPngComCorpoExato(cliente, rota, etagInicial, LimitesFoto.Corpo + 1, informarComprimento);
            await Estado(acima, HttpStatusCode.RequestEntityTooLarge);
        }
        using (var ausente = await cliente.GetAsync(rota)) await Estado(ausente, HttpStatusCode.NotFound);
        Assert.AreEqual(etagInicial, await Versao(cliente, veiculo));
        Assert.AreEqual(0, await banco.FotosObjetos.CountAsync());
        Assert.AreEqual(0, gravacoes);

        var atual = etagInicial;
        foreach (var informarComprimento in new[] { true, false })
        {
            using var noLimite = await EnviarPngComCorpoExato(cliente, rota, atual, LimitesFoto.Corpo, informarComprimento);
            await Estado(noLimite, HttpStatusCode.NoContent);
            atual = noLimite.Headers.ETag!.ToString();
        }
        var anterior = await cliente.GetByteArrayAsync(rota);
        using (var falha = await EnviarFoto(cliente, rota, atual, corrompido))
            await Estado(falha, HttpStatusCode.BadRequest);
        CollectionAssert.AreEqual(anterior, await cliente.GetByteArrayAsync(rota));
        Assert.AreEqual(atual, await Versao(cliente, veiculo));
        Assert.AreEqual(1, await banco.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(1, await banco.FotosObjetos.CountAsync(x => x.Estado == EstadoFoto.Retida));
        Assert.AreEqual(2, await banco.FotosObjetos.CountAsync());
        Assert.AreEqual(2, gravacoes);
        Assert.AreEqual(2, await banco.Auditorias.CountAsync(x => x.EmpresaId == dados.Empresa && x.Evento.StartsWith("foto.")));
        Console.WriteLine("HTTP: 5242880 B aceitos e 5242881 B negados, com/sem Content-Length; arquivo rejeitado não criou objeto nem ativação.");
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Limites_F5_quatro_uploads_concorrentes_nao_entram_na_fila()
    {
        using var volume = new VolumeTeste();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: volume);
        using var a = fabrica.CreateClient();
        using var b = fabrica.CreateClient();
        var dadosA = await EntrarEPreparar(a, fabrica);
        var dadosB = await EntrarEPreparar(b, fabrica);
        var veiculoA = $"/api/v1/veiculos/{dadosA.Veiculo.Id}";
        var veiculoB = $"/api/v1/veiculos/{dadosB.Veiculo.Id}";
        var tagA = await Versao(a, veiculoA);
        var tagB = await Versao(b, veiculoB);
        var entrada = AmostrasFotoLimites.Gerar("jpeg-cores");
        var gravou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gravacoes = 0;
        volume.AposGravar = async (_, ct) =>
        {
            Interlocked.Increment(ref gravacoes);
            gravou.TrySetResult();
            await liberar.Task.WaitAsync(ct);
        };
        var primeiro = EnviarFoto(a, veiculoA + "/foto", tagA, entrada, "image/jpeg");
        try
        {
            await gravou.Task.WaitAsync(TimeSpan.FromSeconds(12));
            var tempos = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                var cronometro = Stopwatch.StartNew();
                using var resposta = await EnviarFoto(b, veiculoB + "/foto", tagB, entrada, "image/jpeg");
                var tempo = cronometro.Elapsed.TotalMilliseconds;
                await Estado(resposta, HttpStatusCode.ServiceUnavailable);
                Assert.AreEqual("foto_processamento_indisponivel",
                    (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
                return tempo;
            })).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.IsFalse(primeiro.IsCompleted, "As recusas devem ocorrer antes de a vaga ser liberada.");
            Assert.AreEqual(1, gravacoes);
            Assert.AreEqual(tagB, await Versao(b, veiculoB));
            using var semFoto = await b.GetAsync(veiculoB + "/foto");
            await Estado(semFoto, HttpStatusCode.NotFound);
            await using var escopo = fabrica.Services.CreateAsyncScope();
            var banco = Banco(escopo, dadosB.Empresa);
            Assert.AreEqual(0, await banco.FotosObjetos.CountAsync());
            Console.WriteLine(FormattableString.Invariant(
                $"CONCORRENCIA entrada={entrada.Length} vagas=1 recusas=4 status=503 min_ms={tempos.Min():F1} max_ms={tempos.Max():F1}"));
        }
        finally { liberar.TrySetResult(); }
        using (var aceito = await primeiro) await Estado(aceito, HttpStatusCode.NoContent);
        using (var repetido = await EnviarFoto(b, veiculoB + "/foto", tagB, AmostrasFotoLimites.Gerar("png-alfa")))
            await Estado(repetido, HttpStatusCode.NoContent);
        Assert.AreEqual(2, gravacoes);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Limites_F5_resultado_apos_o_prazo_cooperativo_nao_ativa_foto()
    {
        using var volume = new VolumeTeste();
        var gravacoes = 0;
        volume.AposGravar = (_, _) => { gravacoes++; return Task.CompletedTask; };
        var saneador = new SaneadorComRetornoTardio();
        using var fabrica = new SqlServerWebApplicationFactory(armazenamentoFotos: volume, saneadorFotos: saneador);
        using var cliente = fabrica.CreateClient();
        var dados = await EntrarEPreparar(cliente, fabrica);
        var veiculo = $"/api/v1/veiculos/{dados.Veiculo.Id}";
        var rota = veiculo + "/foto";
        var etag = await Versao(cliente, veiculo);
        var cronometro = Stopwatch.StartNew();
        using var resposta = await EnviarFoto(cliente, rota, etag);
        var duracao = cronometro.Elapsed;
        await Estado(resposta, HttpStatusCode.ServiceUnavailable);
        Assert.AreEqual("foto_processamento_indisponivel",
            (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.IsTrue(saneador.DevolveuDepoisDoCancelamento);
        Assert.IsGreaterThanOrEqualTo(LimitesFoto.Processamento, duracao);
        Assert.AreEqual(etag, await Versao(cliente, veiculo));
        using (var ausente = await cliente.GetAsync(rota)) await Estado(ausente, HttpStatusCode.NotFound);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.AreEqual(0, await Banco(escopo, dados.Empresa).FotosObjetos.CountAsync());
        Assert.AreEqual(0, gravacoes);
        saneador.SimularAtraso = false;
        using var reenvio = await EnviarFoto(cliente, rota, etag);
        await Estado(reenvio, HttpStatusCode.NoContent);
        Assert.AreEqual(1, gravacoes);
        Console.WriteLine(FormattableString.Invariant(
            $"PRAZO simulacao=retorno-nao-preemptivo janela_s={LimitesFoto.Processamento.TotalSeconds} ms={duracao.TotalMilliseconds:F1} status=503 gravacoes_antes_do_retry=0"));
    }

    private static async Task<HttpResponseMessage> EnviarPngComCorpoExato(HttpClient cliente, string rota,
        string etag, int alvo, bool informarComprimento)
    {
        const string boundary = "TesteF5LimiteExato";
        var inicio = Encoding.ASCII.GetBytes($"--{boundary}\r\nContent-Disposition: form-data; name=\"arquivo\"; filename=\"sintetica.png\"\r\nContent-Type: image/png\r\n\r\n");
        var fim = Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n");
        var png = AmostrasFotoLimites.PngSimples();
        var texto = new byte[alvo - inicio.Length - fim.Length - png.Length - 12];
        Array.Fill(texto, (byte)'x');
        "Teste\0"u8.CopyTo(texto);
        var imagem = AmostrasFotoLimites.InserirChunkPng(png, "tEXt"u8, texto);
        using var pedido = new HttpRequestMessage(HttpMethod.Put, rota);
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        pedido.Content = new CorpoComComprimentoOpcional(inicio, imagem, fim, informarComprimento);
        pedido.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        Assert.AreEqual(alvo, inicio.Length + imagem.Length + fim.Length);
        return await cliente.SendAsync(pedido);
    }

    private sealed class CorpoComComprimentoOpcional(byte[] inicio, byte[] arquivo, byte[] fim, bool informar) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = inicio.Length + arquivo.Length + fim.Length; return informar; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(inicio);
            await stream.WriteAsync(arquivo);
            await stream.WriteAsync(fim);
        }
    }

    private sealed class SaneadorComRetornoTardio : ISaneadorFoto
    {
        public bool SimularAtraso { get; set; } = true;
        public bool DevolveuDepoisDoCancelamento { get; private set; }
        public ImagemSaneada Sanear(EntradaFoto entrada, CancellationToken ct)
        {
            var imagem = new SaneadorFoto().Sanear(entrada, ct);
            if (!SimularAtraso) return imagem;
            // Dobra de teste: um codec síncrono pode ignorar o cancelamento e retornar mais tarde.
            if (!ct.WaitHandle.WaitOne(LimitesFoto.Processamento + TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("A API não sinalizou o prazo cooperativo.");
            DevolveuDepoisDoCancelamento = ct.IsCancellationRequested;
            return imagem;
        }
    }
}
