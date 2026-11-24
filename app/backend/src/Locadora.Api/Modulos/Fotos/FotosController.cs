using Locadora.Application;
using Locadora.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Locadora.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/veiculos/{id:guid}/foto")]
public sealed class FotosController(FotosService fotos) : ControllerBase
{
    [HttpGet]
    [Produces("image/jpeg", "image/png")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var conteudo = await fotos.ObterAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.ContentLength = conteudo.Tamanho;
        return File(conteudo.Fluxo, conteudo.Mime);
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimitesFoto.Corpo)]
    [LeituraDiretaDoMultipart]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Enviar(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        var etag = await fotos.EnviarAsync(id, ifMatch, LerArquivoAsync, ct);
        RespostasHttp.DefinirEtag(Response, etag);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Remover(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        RespostasHttp.DefinirEtag(Response, await fotos.RemoverAsync(id, ifMatch, ct));
        return NoContent();
    }

    private async Task<EntradaFoto> LerArquivoAsync(CancellationToken ct)
    {
        if (Request.ContentLength > LimitesFoto.Corpo) throw FalhaFotoException.Limite();
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var tipo) ||
            !string.Equals(tipo.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw FalhaFotoException.Formato();
        var boundary = HeaderUtilities.RemoveQuotes(tipo.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 70) throw ArquivoInvalido();

        var corpo = new FluxoDeCorpoLimitado(Request.Body);
        try
        {
            var leitor = new MultipartReader(boundary, corpo);
            var parte = await leitor.ReadNextSectionAsync(ct);
            if (parte is null || !ContentDispositionHeaderValue.TryParse(parte.ContentDisposition, out var disposicao) ||
                !disposicao.IsFileDisposition() || HeaderUtilities.RemoveQuotes(disposicao.Name).Value != "arquivo")
                throw ArquivoInvalido();
            var mime = parte.ContentType ?? string.Empty;
            if (!mime.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) &&
                !mime.Equals("image/png", StringComparison.OrdinalIgnoreCase)) throw FalhaFotoException.Formato();
            using var bytes = new MemoryStream();
            await parte.Body.CopyToAsync(bytes, ct);
            if (await leitor.ReadNextSectionAsync(ct) is not null) throw ArquivoInvalido();
            // Contar também o restante do corpo, inclusive o epílogo multipart em servidores sem limite nativo.
            await corpo.CopyToAsync(Stream.Null, ct);
            return new(bytes.ToArray(), mime);
        }
        catch (BadHttpRequestException e) when (e.StatusCode == 413) { throw FalhaFotoException.Limite(); }
        catch (InvalidDataException) { throw ArquivoInvalido(); }
        catch (IOException) when (!ct.IsCancellationRequested) { throw ArquivoInvalido(); }
    }

    private static ValidacaoException ArquivoInvalido() => new("arquivo", "Envie um único arquivo JPEG ou PNG no campo arquivo.");

    private sealed class FluxoDeCorpoLimitado(Stream origem) : Stream
    {
        private int lidos;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = await origem.ReadAsync(buffer[..Math.Min(buffer.Length, LimitesFoto.Corpo - lidos + 1)], cancellationToken);
            lidos += n;
            if (lidos > LimitesFoto.Corpo) throw FalhaFotoException.Limite();
            return n;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class LeituraDiretaDoMultipartAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // O binding automático não deve consumir nem armazenar o multipart antes da precondição do veículo.
        var fabricas = context.ValueProviderFactories;
        for (var i = fabricas.Count - 1; i >= 0; i--)
            if (fabricas[i] is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory)
                fabricas.RemoveAt(i);
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
