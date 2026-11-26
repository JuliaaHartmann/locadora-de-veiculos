using System.Globalization;

namespace Locadora.Domain;

public abstract class EntidadeNumerada : EntidadeEmpresa
{
    public int NumeroExibicao { get; set; }

    public static string FormatarCodigo(int numero) => numero.ToString("D4", CultureInfo.InvariantCulture);
}
