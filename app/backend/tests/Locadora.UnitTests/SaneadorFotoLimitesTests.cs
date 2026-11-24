using System.Diagnostics;
using Locadora.Application;
using Locadora.Infrastructure;
using Locadora.Testes;
using SkiaSharp;

namespace Locadora.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class SaneadorFotoLimitesTests
{
    private const string VariavelAmostras = "LOCADORA_F5_AMOSTRAS";

    [TestMethod]
    [TestCategory("FotoAmostrasF5")]
    public void Gerar_amostras_sinteticas_em_diretorio_exclusivo()
    {
        var pasta = PastaAmostras();
        foreach (var caso in AmostrasFotoLimites.Casos)
        {
            var bytes = AmostrasFotoLimites.Gerar(caso);
            using var arquivo = new FileStream(Path.Combine(pasta, caso + ".bin"), FileMode.CreateNew);
            arquivo.Write(bytes);
            Console.WriteLine($"AMOSTRA caso={caso} bytes={bytes.Length}");
        }
    }

    [TestMethod]
    [TestCategory("FotoLimitesF5")]
    [DataRow("png-rgb")]
    [DataRow("png-alfa")]
    [DataRow("jpeg-cores")]
    [DataRow("jpeg-cinza")]
    public void Medir_saneamento_com_entrada_proxima_dos_dois_limites(string caso)
    {
        var bytes = File.ReadAllBytes(Path.Combine(PastaAmostras(), caso + ".bin"));
        Assert.IsInRange(AmostrasFotoLimites.MinimoBytes, AmostrasFotoLimites.MaximoBytes, bytes.Length);
        var (largura, altura, mime) = AmostrasFotoLimites.Descrever(caso);
        Assert.IsInRange(15_900_000L, (long)LimitesFoto.Pixels, (long)largura * altura);
        var saneador = new SaneadorFoto();
        var entrada = new EntradaFoto(bytes, mime);

        for (var rodada = 1; rodada <= 3; rodada++)
        {
            using var processo = Process.GetCurrentProcess();
            processo.Refresh();
            var antes = processo.WorkingSet64;
            var pico = antes;
            using var parou = new ManualResetEventSlim();
            using var iniciou = new ManualResetEventSlim();
            var monitor = new Thread(() =>
            {
                using var amostrador = Process.GetCurrentProcess();
                iniciou.Set();
                do
                {
                    amostrador.Refresh();
                    pico = Math.Max(pico, amostrador.WorkingSet64);
                } while (!parou.Wait(TimeSpan.FromMilliseconds(10)));
            }) { IsBackground = true };
            monitor.Start();
            ImagemSaneada saida;
            long alocacao;
            TimeSpan tempo;
            try
            {
                iniciou.Wait();
                using var prazo = new CancellationTokenSource(LimitesFoto.Processamento);
                var alocadoAntes = GC.GetAllocatedBytesForCurrentThread();
                var cronometro = Stopwatch.StartNew();
                saida = saneador.Sanear(entrada, prazo.Token);
                tempo = cronometro.Elapsed;
                alocacao = GC.GetAllocatedBytesForCurrentThread() - alocadoAntes;
                prazo.Token.ThrowIfCancellationRequested();
            }
            finally { parou.Set(); monitor.Join(); }

            Assert.AreEqual(mime, saida.Mime);
            Assert.IsInRange(1, LimitesFoto.Saida, saida.Bytes.Count);
            using var dados = new MemoryStream(saida.Bytes.Array!, saida.Bytes.Offset, saida.Bytes.Count, writable: false);
            using var codec = SKCodec.Create(dados);
            Assert.IsNotNull(codec);
            Assert.AreEqual((largura, altura), (codec.Info.Width, codec.Info.Height));
            Console.WriteLine(FormattableString.Invariant(
                $"MEDICAO caso={caso} rodada={rodada} pixels={(long)largura * altura} entrada={bytes.Length} saida={saida.Bytes.Count} ms={tempo.TotalMilliseconds:F1} rss_base={antes} rss_pico={pico} alocado_gc_thread={alocacao}"));
        }
    }

    private static string PastaAmostras()
    {
        var pasta = Environment.GetEnvironmentVariable(VariavelAmostras);
        if (string.IsNullOrEmpty(pasta))
            Assert.Inconclusive($"Ensaio opt-in: defina {VariavelAmostras} para um diretório exclusivo de amostras sintéticas.");
        Assert.IsTrue(Path.IsPathFullyQualified(pasta) && Directory.Exists(pasta));
        return pasta;
    }
}
