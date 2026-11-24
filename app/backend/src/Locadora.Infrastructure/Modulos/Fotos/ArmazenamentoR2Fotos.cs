using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Locadora.Application;
using Locadora.Domain;

namespace Locadora.Infrastructure;

public sealed class ArmazenamentoR2Fotos : IArmazenamentoFotos, IDisposable
{
    private readonly AmazonS3Client cliente;
    private readonly string bucket;
    public string Deposito { get; }

    public ArmazenamentoR2Fotos(string endpoint, string bucket, string acesso, string segredo)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort ||
            uri.HostNameType != UriHostNameType.Dns || !uri.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal) ||
            uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(bucket) || bucket.Length > 63 ||
            bucket.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) ||
            string.IsNullOrWhiteSpace(acesso) || string.IsNullOrWhiteSpace(segredo))
            throw new InvalidOperationException("Configuração privada de fotos R2 inválida ou incompleta.");
        this.bucket = bucket;
        Deposito = $"r2:{uri.Host}/{bucket}";
        if (Deposito.Length > 300) throw new InvalidOperationException("Identificador do depósito de fotos inválido.");
        cliente = new AmazonS3Client(new BasicAWSCredentials(acesso, segredo), new AmazonS3Config
        {
            ServiceURL = uri.GetLeftPart(UriPartial.Authority), AuthenticationRegion = "auto", ForcePathStyle = true,
            MaxErrorRetry = 0, RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    private void Conferir(FotoObjeto objeto)
    {
        if (objeto.Deposito != Deposito) throw FalhaFotoException.Indisponivel();
    }

    public async Task GravarAsync(FotoObjeto objeto, ArraySegment<byte> bytes, CancellationToken ct)
    {
        Conferir(objeto);
        try
        {
            using var corpo = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count, writable: false);
            await cliente.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = objeto.Chave, InputStream = corpo, ContentType = objeto.Mime,
                DisablePayloadSigning = true, DisableDefaultChecksumValidation = true,
            }, ct);
        }
        catch (Exception erro) when (ErroDeProvedor(erro)) { throw FalhaFotoException.Indisponivel(); }
    }

    public async Task<Stream?> LerAsync(FotoObjeto objeto, CancellationToken ct)
    {
        Conferir(objeto);
        try
        {
            var resposta = await cliente.GetObjectAsync(bucket, objeto.Chave, ct);
            if (resposta.ContentLength != objeto.Tamanho)
            {
                resposta.Dispose();
                throw FalhaFotoException.Indisponivel();
            }
            // A API descarta o fluxo da resposta e libera a conexão HTTP ao terminar a leitura.
            return resposta.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
        catch (Exception erro) when (ErroDeProvedor(erro)) { throw FalhaFotoException.Indisponivel(); }
    }

    public async Task RemoverAsync(FotoObjeto objeto, CancellationToken ct)
    {
        Conferir(objeto);
        try { await cliente.DeleteObjectAsync(bucket, objeto.Chave, ct); }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { }
        catch (Exception erro) when (ErroDeProvedor(erro)) { throw FalhaFotoException.Indisponivel(); }
    }

    private static bool ErroDeProvedor(Exception erro) =>
        erro is AmazonServiceException or AmazonClientException or HttpRequestException or IOException;

    public void Dispose() => cliente.Dispose();
}
