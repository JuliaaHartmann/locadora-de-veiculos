using Locadora.Domain;

namespace Locadora.Application;

public sealed record Pagina<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record RespostaComVersao<T>(T Valor, string Etag);

public sealed class NaoEncontradoException(string mensagem = "Recurso não encontrado.") : Exception(mensagem);

public sealed class ConflitoException(string mensagem, string? campo = null) : Exception(mensagem)
{
    public string? Campo { get; } = campo;
}

public sealed class PrecondicaoException() : Exception("A versão informada não corresponde ao cadastro atual.");

public sealed class PrecondicaoObrigatoriaException() : Exception("Informe If-Match com a versão atual do cadastro.");

public static class Paginacao
{
    public const int Padrao = 20;
    public const int Maximo = 100;

    public static (int Pagina, int Tamanho) Normalizar(int? page, int? pageSize)
    {
        var pagina = page ?? 1;
        var tamanho = pageSize ?? Padrao;
        var erros = new List<ErroValidacao>();
        if (pagina < 1) erros.Add(new ErroValidacao("page", "Informe uma página a partir de 1."));
        if (tamanho < 1 || tamanho > Maximo)
            erros.Add(new ErroValidacao("pageSize", $"Informe um tamanho entre 1 e {Maximo}."));
        if (pagina > 0 && tamanho > 0 && (long)(pagina - 1) * tamanho > int.MaxValue)
            erros.Add(new ErroValidacao("page", "Página acima do limite suportado."));
        if (erros.Count > 0) throw new ValidacaoException(erros);
        return (pagina, tamanho);
    }
}

public static class Etag
{
    public static string De(byte[] versao) => Convert.ToBase64String(versao);

    public static string Cabecalho(byte[] versao) => $"\"{De(versao)}\"";

    public static void Exigir(string? ifMatch, byte[] versao)
    {
        if (string.IsNullOrWhiteSpace(ifMatch)) throw new PrecondicaoObrigatoriaException();
        var valor = ifMatch.Trim();
        if (valor.StartsWith("W/", StringComparison.Ordinal)) throw new PrecondicaoException();
        valor = valor.Trim('"');
        if (!string.Equals(valor, De(versao), StringComparison.Ordinal)) throw new PrecondicaoException();
    }
}

/// <summary>
/// Porta transacional da escrita: inicia transação, serializa pela linha da Empresa, executa a mutação,
/// grava a auditoria no mesmo commit e confirma. Application/Domain não conhecem EF nem HTTP.
/// </summary>
public interface IProtocoloEscrita
{
    Guid EmpresaId { get; }
    Guid? UsuarioId { get; }
    Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken cancellationToken);
    void Auditar(string evento, string entidadeTipo, Guid entidadeId, params string[] campos);
}
