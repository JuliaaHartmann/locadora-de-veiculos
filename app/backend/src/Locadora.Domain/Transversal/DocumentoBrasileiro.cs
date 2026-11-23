using System.Globalization;

namespace Locadora.Domain;

/// <summary>Normalização e verificação dos dígitos de CPF e CNPJ numéricos previstos no PRD.</summary>
public static class DocumentoBrasileiro
{
    public static string Normalizar(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool CpfValido(string? valor)
    {
        var cpf = Normalizar(valor);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;
        var primeiro = Digito(cpf[..9], [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var segundo = Digito(cpf[..9] + primeiro, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cpf == cpf[..9] + primeiro + segundo;
    }

    public static bool CnpjValido(string? valor)
    {
        var cnpj = Normalizar(valor);
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1) return false;
        var primeiro = Digito(cnpj[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var segundo = Digito(cnpj[..12] + primeiro, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cnpj == cnpj[..12] + primeiro + segundo;
    }

    private static char Digito(string baseNumerica, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (baseNumerica[i] - '0') * pesos[i];
        var resto = soma % 11;
        return (resto < 2 ? 0 : 11 - resto).ToString(CultureInfo.InvariantCulture)[0];
    }
}
