namespace Locadora.Api;

internal static class RespostasHttp
{
    public static Microsoft.AspNetCore.Mvc.OkObjectResult ComVersao<T>(
        HttpResponse resposta, Locadora.Application.RespostaComVersao<T>? resultado)
    {
        if (resultado is null) throw new Locadora.Application.NaoEncontradoException();
        DefinirEtag(resposta, resultado.Etag);
        return new(resultado.Valor);
    }

    public static void DefinirEtag(HttpResponse resposta, string etag) =>
        resposta.Headers.ETag = $"\"{etag}\"";
}
