using System.Buffers.Binary;
using Locadora.Application;
using Locadora.Domain;
using SkiaSharp;

namespace Locadora.Infrastructure;

public sealed class SaneadorFoto : ISaneadorFoto
{
    private static ReadOnlySpan<byte> AssinaturaPng => [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] TabelaCrc = Enumerable.Range(0, 256).Select(i =>
    {
        var crc = (uint)i;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        return crc;
    }).ToArray();

    public ImagemSaneada Sanear(EntradaFoto entrada, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dados = entrada.Bytes.AsSpan();
        if (dados.IsEmpty) throw Invalida();
        if (dados.Length > LimitesFoto.Corpo) throw FalhaFotoException.Limite();
        var formato = dados.StartsWith(AssinaturaPng) ? SKEncodedImageFormat.Png
            : dados.StartsWith(new byte[] { 0xff, 0xd8, 0xff }) ? SKEncodedImageFormat.Jpeg
            : throw FalhaFotoException.Formato();
        var mime = formato == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg";
        if (!string.Equals(entrada.Mime, mime, StringComparison.OrdinalIgnoreCase)) throw FalhaFotoException.Formato();
        if (formato == SKEncodedImageFormat.Png) ValidarPng(dados, ct);
        else if (!dados.EndsWith(new byte[] { 0xff, 0xd9 })) throw Invalida();

        using var origem = new MemoryStream(entrada.Bytes, writable: false);
        using var codec = SKCodec.Create(origem) ?? throw Invalida();
        if (codec.EncodedFormat != formato) throw FalhaFotoException.Formato();
        var (largura, altura) = (codec.Info.Width, codec.Info.Height);
        ValidarDimensoes(largura, altura);
        if (codec.FrameCount > 1) throw Invalida();
        var info = new SKImageInfo(largura, altura, SKColorType.Rgba8888,
            formato == SKEncodedImageFormat.Jpeg ? SKAlphaType.Opaque : SKAlphaType.Premul);
        using var bitmap = new SKBitmap();
        ct.ThrowIfCancellationRequested();
        if (!bitmap.TryAllocPixels(info) || codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw Invalida();
        ct.ThrowIfCancellationRequested();
        // Não vincular o perfil/EXIF de origem ao buffer reencodado.
        using var saida = new SaidaLimitada(ct);
        using var fluxo = new SKManagedWStream(saida);
        var ok = bitmap.Encode(fluxo, formato, 90);
        ct.ThrowIfCancellationRequested();
        if (!ok || saida.Excedida || !saida.TryGetBuffer(out var bytes) || bytes.Count == 0)
            throw new ValidacaoException("arquivo", "Não foi possível gerar uma imagem dentro dos limites de processamento.");
        return new(bytes, mime);
    }

    private static void ValidarPng(ReadOnlySpan<byte> dados, CancellationToken ct)
    {
        dados = dados[AssinaturaPng.Length..];
        var primeiro = true;
        var temPixels = false;
        while (dados.Length >= 12)
        {
            ct.ThrowIfCancellationRequested();
            var tamanho = BinaryPrimitives.ReadUInt32BigEndian(dados);
            if (tamanho > dados.Length - 12) throw Invalida();
            var n = (int)tamanho;
            var tipo = dados.Slice(4, 4);
            var conteudo = dados.Slice(8, n);
            if (primeiro)
            {
                if (!tipo.SequenceEqual("IHDR"u8) || n != 13) throw Invalida();
                ValidarDimensoes(BinaryPrimitives.ReadUInt32BigEndian(conteudo), BinaryPrimitives.ReadUInt32BigEndian(conteudo[4..]));
                primeiro = false;
            }
            else if (tipo.SequenceEqual("IHDR"u8)) throw Invalida();
            if (tipo.SequenceEqual("acTL"u8) || tipo.SequenceEqual("fcTL"u8) || tipo.SequenceEqual("fdAT"u8)) throw Invalida();
            if (tipo.SequenceEqual("IDAT"u8)) temPixels = true;
            var crc = uint.MaxValue;
            foreach (var b in dados.Slice(4, n + 4)) crc = (crc >> 8) ^ TabelaCrc[(crc ^ b) & 255];
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(dados[(n + 8)..])) throw Invalida();
            dados = dados[(n + 12)..];
            if (tipo.SequenceEqual("IEND"u8))
            {
                if (n != 0 || !dados.IsEmpty || !temPixels) throw Invalida();
                return;
            }
        }
        throw Invalida();
    }

    private static void ValidarDimensoes(long largura, long altura)
    {
        if (largura <= 0 || altura <= 0 || largura > LimitesFoto.Pixels / altura)
            throw new ValidacaoException("arquivo", "A imagem deve ter dimensões positivas e no máximo 16 megapixels.");
    }

    private static ValidacaoException Invalida() =>
        new("arquivo", "A imagem está vazia, corrompida ou não é uma imagem estática válida.");

    private sealed class SaidaLimitada(CancellationToken ct) : MemoryStream
    {
        public bool Excedida { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var fim = Position + count;
            if (ct.IsCancellationRequested || fim > LimitesFoto.Saida) { Excedida = true; return; }
            if (Excedida) return;
            if (fim > Capacity)
                Capacity = Math.Min(LimitesFoto.Saida, Math.Max((int)fim, Math.Max(4096, Capacity * 2)));
            base.Write(buffer, offset, count);
        }
    }
}
