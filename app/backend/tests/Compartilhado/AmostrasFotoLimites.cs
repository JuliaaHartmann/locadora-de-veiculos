using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Locadora.Application;
using SkiaSharp;

namespace Locadora.Testes;

internal static class AmostrasFotoLimites
{
    public const int MinimoBytes = 5_000_000;
    public const int MaximoBytes = LimitesFoto.Corpo - 8 * 1024;

    public static readonly string[] Casos = ["png-rgb", "png-alfa", "jpeg-cores", "jpeg-cinza"];

    public static (int Largura, int Altura, string Mime) Descrever(string caso) => caso switch
    {
        "png-rgb" => (4000, 4000, "image/png"),
        "png-alfa" => (5000, 3199, "image/png"),
        "jpeg-cores" => (4000, 4000, "image/jpeg"),
        "jpeg-cinza" => (5000, 3199, "image/jpeg"),
        _ => throw new ArgumentOutOfRangeException(nameof(caso)),
    };

    public static byte[] Gerar(string caso)
    {
        var (largura, altura, mime) = Descrever(caso);
        var alfa = caso == "png-alfa";
        var cinza = caso == "jpeg-cinza";
        var formato = mime == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        var qualidade = cinza ? 74 : 84;
        using var bitmap = new SKBitmap(new SKImageInfo(largura, altura, SKColorType.Bgra8888,
            alfa ? SKAlphaType.Premul : SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        var linha = new int[largura];
        var inicio = 0;
        var fim = altura;
        for (var tentativa = 0; tentativa < 14 && inicio <= fim; tentativa++)
        {
            var linhasRuidosas = inicio + (fim - inicio) / 2;
            canvas.Clear(alfa ? new SKColor(100, 140, 176, 95) : new SKColor(91, 124, 157));
            // Entropia nos pixels, não em metadados; a quantidade de linhas ajusta o tamanho comprimido.
            for (var y = 0; y < linhasRuidosas; y++)
            {
                var estado = (uint)(y + 1) * 2654435761u;
                for (var x = 0; x < largura; x++)
                {
                    estado ^= estado << 13;
                    estado ^= estado >> 17;
                    estado ^= estado << 5;
                    var a = alfa ? 64u + (estado >> 24) % 192u : 255u;
                    var r = (estado & 255) * a / 255;
                    var g = cinza ? r : ((estado >> 8) & 255) * a / 255;
                    var b = cinza ? r : ((estado >> 16) & 255) * a / 255;
                    linha[x] = unchecked((int)((a << 24) | (r << 16) | (g << 8) | b));
                }
                Marshal.Copy(linha, 0, IntPtr.Add(bitmap.GetPixels(), y * bitmap.RowBytes), largura);
            }
            using var dados = bitmap.Encode(formato, qualidade);
            if (dados.Size >= MinimoBytes && dados.Size <= MaximoBytes) return dados.ToArray();
            if (dados.Size < MinimoBytes) inicio = linhasRuidosas + 1;
            else fim = linhasRuidosas - 1;
        }
        throw new InvalidOperationException($"Não foi possível gerar a amostra {caso} na faixa prevista.");
    }

    public static byte[] PngSimples(int largura = 8, int altura = 8)
    {
        using var bitmap = new SKBitmap(largura, altura);
        bitmap.Erase(SKColors.CadetBlue);
        using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return dados.ToArray();
    }

    public static byte[] InserirChunkPng(byte[] png, ReadOnlySpan<byte> tipo, ReadOnlySpan<byte> conteudo)
    {
        const int depoisDoIhdr = 33;
        var parte = new byte[12 + conteudo.Length];
        BinaryPrimitives.WriteInt32BigEndian(parte, conteudo.Length);
        tipo.CopyTo(parte.AsSpan(4));
        conteudo.CopyTo(parte.AsSpan(8));
        var crc = uint.MaxValue;
        foreach (var b in parte.AsSpan(4, conteudo.Length + 4))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(parte.AsSpan(8 + conteudo.Length), ~crc);
        return [.. png.AsSpan(0, depoisDoIhdr), .. parte, .. png.AsSpan(depoisDoIhdr)];
    }
}
