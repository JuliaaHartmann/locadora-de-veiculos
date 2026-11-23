using System.Globalization;
using System.Text.RegularExpressions;

namespace Locadora.Domain;

/// <summary>
/// Conversão dos decimais canônicos do contrato (ponto decimal, sem milhar, expoente ou sinal).
/// A tela converte a vírgula local antes de enviar; o servidor rejeita formatos fora do contrato.
/// </summary>
public static partial class Decimais
{
    public const decimal MaximoMonetario = 9999999999999.999999m;
    public const decimal MaximoCapacidade = 999999999.999m;

    public static decimal? Monetario(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        if (!FormatoMonetario().IsMatch(texto))
            throw new ValidacaoException(campo, "Informe um valor decimal não negativo com até 6 casas.");
        var numero = decimal.Parse(texto, CultureInfo.InvariantCulture);
        if (numero > MaximoMonetario)
            throw new ValidacaoException(campo, "Valor acima do limite permitido.");
        return numero;
    }

    public static decimal? Capacidade(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        if (!FormatoCapacidade().IsMatch(texto))
            throw new ValidacaoException(campo, "Informe uma capacidade decimal com até 3 casas.");
        var numero = decimal.Parse(texto, CultureInfo.InvariantCulture);
        if (numero > MaximoCapacidade)
            throw new ValidacaoException(campo, "Capacidade acima do limite permitido.");
        return numero;
    }

    public static string FormatacaoMonetaria(decimal valor) =>
        valor.ToString("0.######", CultureInfo.InvariantCulture);

    public static string FormatacaoCapacidade(decimal valor) =>
        valor.ToString("0.###", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^[0-9]{1,13}(\.[0-9]{1,6})?\z")]
    private static partial Regex FormatoMonetario();

    [GeneratedRegex(@"^[0-9]{1,9}(\.[0-9]{1,3})?\z")]
    private static partial Regex FormatoCapacidade();
}
