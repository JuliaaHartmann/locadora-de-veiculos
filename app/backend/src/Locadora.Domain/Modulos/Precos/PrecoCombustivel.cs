namespace Locadora.Domain;

/// <summary>
/// Versão imutável de preço de combustível. A vigência e a ordem serial são atribuídas pelo servidor;
/// o cliente nunca altera datas ou versões anteriores.
/// </summary>
public sealed class PrecoCombustivel : EntidadeNumerada
{
    public Combustivel Tipo { get; set; }
    public decimal Preco { get; set; }
    public DateTimeOffset InicioVigenciaUtc { get; set; }
    public long Ordem { get; set; }

    public static PrecoCombustivel Criar(Combustivel tipo, decimal? preco, DateTimeOffset inicioVigenciaUtc,
        long ordem, Guid empresaId, DateTimeOffset agora)
    {
        var erros = new List<ErroValidacao>();
        if (!Enum.IsDefined(tipo))
            erros.Add(new ErroValidacao("tipo", "Selecione um combustível válido."));
        Validador.Adicionar(erros, "preco", Validador.DecimalNaoNegativo(preco));
        if (ordem <= 0) erros.Add(new ErroValidacao("ordem", "Ordem serial inválida."));
        Validador.Lancar(erros);
        return new PrecoCombustivel
        {
            Tipo = tipo,
            Preco = preco!.Value,
            InicioVigenciaUtc = inicioVigenciaUtc,
            Ordem = ordem,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }
}
