using System.Globalization;
using Locadora.Domain;

namespace Locadora.Application;

public static class HorarioLocacao
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly string[] Formatos = ["yyyy-MM-dd'T'HH:mmzzz", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    public static DateTimeOffset ParaSaoPaulo(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Fuso);

    public static DateTimeOffset InterpretarRetorno(string? texto, string campo = "retornoPrevisto")
    {
        if (!DateTimeOffset.TryParseExact(texto, Formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retorno))
            throw new ValidacaoException(campo, "Informe data e hora ISO 8601 com o deslocamento do horário de São Paulo.");

        // Na volta do horário de verão os dois offsets históricos são distinguíveis pelo instante.
        // A conversão de ida e volta também rejeita horários de parede que nunca existiram.
        if (ParaSaoPaulo(retorno).Offset != retorno.Offset)
            throw new ValidacaoException(campo, "O horário ou deslocamento não corresponde ao fuso de São Paulo.");
        return retorno;
    }

    public static DateTimeOffset InicioDia(DateOnly dia, string campo)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        // Se a meia-noite tiver sido pulada, o dia começa no primeiro minuto local válido.
        while (Fuso.IsInvalidTime(local))
            local = local.AddMinutes(1);
        var offset = Fuso.IsAmbiguousTime(local) ? Fuso.GetAmbiguousTimeOffsets(local).Max() : Fuso.GetUtcOffset(local);
        try { return new DateTimeOffset(local, offset).ToUniversalTime(); }
        catch (ArgumentException) { throw new ValidacaoException(campo, "Data fora do intervalo suportado."); }
    }
}
