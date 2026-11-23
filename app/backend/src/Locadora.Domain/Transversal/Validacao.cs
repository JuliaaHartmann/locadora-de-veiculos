namespace Locadora.Domain;

public sealed record ErroValidacao(string Campo, string Mensagem);

public sealed class ValidacaoException : Exception
{
    public IReadOnlyList<ErroValidacao> Erros { get; }

    public ValidacaoException(IEnumerable<ErroValidacao> erros) : base("Dados inválidos.")
        => Erros = erros.ToArray();

    public ValidacaoException(string campo, string mensagem) : this([new ErroValidacao(campo, mensagem)]) { }
}

/// <summary>Validações de forma compartilhadas pelas entidades; mensagens são associadas ao campo.</summary>
public static class Validador
{
    public static string? Obrigatorio(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "Informe este campo.";
        return valor.Trim().Length > maximo ? $"Use no máximo {maximo} caracteres." : null;
    }

    public static string? Opcional(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor.Trim().Length > maximo ? $"Use no máximo {maximo} caracteres." : null;
    }

    public static string? DecimalNaoNegativo(decimal? valor)
    {
        if (valor is null) return "Informe este campo.";
        return valor < 0 ? "Informe um valor não negativo." : null;
    }

    public static string? DecimalPositivo(decimal? valor)
    {
        if (valor is null) return "Informe este campo.";
        return valor <= 0 ? "Informe um valor maior que zero." : null;
    }

    public static string? InteiroNaoNegativo(int? valor)
    {
        if (valor is null) return "Informe este campo.";
        return valor < 0 ? "Informe um inteiro não negativo." : null;
    }

    public static string? EmailObrigatorio(string? valor)
    {
        var mensagem = Obrigatorio(valor, 254);
        return mensagem ?? (EmailValido(valor) ? null : "Informe um e-mail válido.");
    }

    public static string? EmailOpcional(string? valor)
    {
        var mensagem = Opcional(valor, 254);
        return mensagem ?? (string.IsNullOrWhiteSpace(valor) || EmailValido(valor) ? null : "Informe um e-mail válido.");
    }

    public static string? DataObrigatoria(DateOnly? valor) =>
        valor is null || valor == default ? "Informe uma data válida." : null;

    private static bool EmailValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        var arroba = valor.IndexOf('@');
        return arroba > 0 && arroba < valor.Length - 3 && valor.IndexOf('.', arroba) > arroba + 1 &&
            !valor.Any(char.IsWhiteSpace);
    }

    public static void Adicionar(List<ErroValidacao> erros, string campo, string? mensagem)
    {
        if (mensagem is not null) erros.Add(new ErroValidacao(campo, mensagem));
    }

    public static void Lancar(List<ErroValidacao> erros)
    {
        if (erros.Count > 0) throw new ValidacaoException(erros);
    }
}
