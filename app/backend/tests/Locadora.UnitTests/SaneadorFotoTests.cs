using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Locadora.Application;
using Locadora.Domain;
using Locadora.Infrastructure;
using SkiaSharp;

namespace Locadora.UnitTests;

[TestClass]
public sealed class SaneadorFotoTests
{
    private static byte[] Gerar(SKEncodedImageFormat formato, int largura = 8, int altura = 8)
    {
        using var bitmap = new SKBitmap(largura, altura);
        bitmap.Erase(SKColors.Blue);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(formato, 90);
        return dados.ToArray();
    }

    [TestMethod]
    [DataRow("image/jpeg", SKEncodedImageFormat.Jpeg)]
    [DataRow("image/png", SKEncodedImageFormat.Png)]
    public void Decodifica_e_reencoda_com_mime_real_e_sem_metadados_de_origem(string mime, SKEncodedImageFormat formato)
    {
        var origem = Gerar(formato);
        var marcador = "metadado-sintetico-F5"u8.ToArray();
        origem = formato == SKEncodedImageFormat.Png
            ? InserirPng(origem, 33, "tEXt"u8, [.. "Comentario\0"u8, .. marcador])
            : [.. origem.AsSpan(0, 2), 0xff, 0xe1, 0, (byte)(8 + marcador.Length), .. "Exif\0\0"u8, .. marcador, .. origem.AsSpan(2)];
        var saida = new SaneadorFoto().Sanear(new(origem, mime), default);
        Assert.AreEqual(mime, saida.Mime);
        Assert.IsLessThan(0, saida.Bytes.AsSpan().IndexOf(marcador));
        using var codec = SKCodec.Create(new MemoryStream(saida.Bytes.ToArray()));
        Assert.IsNotNull(codec);
        Assert.AreEqual(formato, codec.EncodedFormat);
        Assert.AreEqual(8, codec.Info.Width);
    }

    [TestMethod]
    public void Rejeita_dimensoes_e_assinatura_antes_do_decode_e_nao_aceita_corrupcao_ou_animacao()
    {
        var saneador = new SaneadorFoto();
        var png = Gerar(SKEncodedImageFormat.Png);
        var jpeg = Gerar(SKEncodedImageFormat.Jpeg);
        var largo = png.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(largo.AsSpan(16), 16_000_001);
        Assert.AreEqual("arquivo", Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new(largo, "image/png"), default)).Erros[0].Campo);
        Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new(png[..^1], "image/png"), default));
        Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new(jpeg[..^1], "image/jpeg"), default));
        Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new([], "image/png"), default));
        Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new(InserirPng(png, 33, "acTL"u8, new byte[8]), "image/png"), default));
        png[30] ^= 0x01;
        Assert.ThrowsExactly<ValidacaoException>(() => saneador.Sanear(new(png, "image/png"), default));
        Assert.AreEqual(TipoFalhaFoto.Formato, Assert.ThrowsExactly<FalhaFotoException>(() => saneador.Sanear(new(jpeg, "image/png"), default)).Tipo);
        Assert.AreEqual(TipoFalhaFoto.Formato, Assert.ThrowsExactly<FalhaFotoException>(() => saneador.Sanear(new("GIF89a"u8.ToArray(), "image/png"), default)).Tipo);
        Assert.AreEqual(TipoFalhaFoto.LimiteCorpo, Assert.ThrowsExactly<FalhaFotoException>(() => saneador.Sanear(new(new byte[LimitesFoto.Corpo + 1], "image/png"), default)).Tipo);
    }

    [TestMethod]
    public void Ensaio_local_na_fronteira_de_pixels_e_com_cancelamento()
    {
        var png = Gerar(SKEncodedImageFormat.Png, 4000, 4000);
        var cronometro = Stopwatch.StartNew();
        using var prazo = new CancellationTokenSource(LimitesFoto.Processamento);
        var resultado = new SaneadorFoto().Sanear(new(png, "image/png"), prazo.Token);
        Assert.IsLessThanOrEqualTo(LimitesFoto.Saida, resultado.Bytes.Count);
        using var codec = SKCodec.Create(new MemoryStream(resultado.Bytes.ToArray()));
        Assert.AreEqual(16_000_000, codec!.Info.Width * codec.Info.Height);
        Console.WriteLine($"Ensaio sintético 4000×4000: {png.Length} B entrada, {resultado.Bytes.Count} B saída, {cronometro.ElapsedMilliseconds} ms.");
        prazo.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new SaneadorFoto().Sanear(new(png, "image/png"), prazo.Token));
    }

    private static byte[] InserirPng(byte[] png, int posicao, ReadOnlySpan<byte> tipo, ReadOnlySpan<byte> dados)
    {
        var parte = new byte[12 + dados.Length];
        BinaryPrimitives.WriteInt32BigEndian(parte, dados.Length);
        tipo.CopyTo(parte.AsSpan(4));
        dados.CopyTo(parte.AsSpan(8));
        var crc = uint.MaxValue;
        foreach (var b in parte.AsSpan(4, dados.Length + 4))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        BinaryPrimitives.WriteUInt32BigEndian(parte.AsSpan(8 + dados.Length), ~crc);
        return [.. png.AsSpan(0, posicao), .. parte, .. png.AsSpan(posicao)];
    }
}
