namespace Locadora.Domain;

public enum TipoSeguro
{
    Cliente,
    Terceiros,
}

/// <summary>Preço diário independente por tipo de seguro. Ausência de registro difere de preço zero.</summary>
public sealed class SeguroConfiguracao : EntidadeEmpresa
{
    public TipoSeguro Tipo { get; set; }
    public decimal PrecoDiario { get; set; }

    public static SeguroConfiguracao Criar(TipoSeguro tipo, decimal? precoDiario, Guid empresaId, DateTimeOffset agora)
    {
        var dados = Normalizar(tipo, precoDiario);
        return new SeguroConfiguracao
        {
            Tipo = dados.Tipo,
            PrecoDiario = dados.Preco,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void AlterarPreco(decimal? precoDiario, DateTimeOffset agora)
    {
        var dados = Normalizar(Tipo, precoDiario);
        PrecoDiario = dados.Preco;
        AtualizadoEm = agora;
    }

    private static Dados Normalizar(TipoSeguro tipo, decimal? precoDiario)
    {
        var erros = new List<ErroValidacao>();
        if (!Enum.IsDefined(tipo))
            erros.Add(new ErroValidacao("tipo", "Selecione seguro do cliente ou de terceiros."));
        Validador.Adicionar(erros, "precoDiario", Validador.DecimalNaoNegativo(precoDiario));
        Validador.Lancar(erros);
        return new Dados(tipo, precoDiario!.Value);
    }

    private sealed record Dados(TipoSeguro Tipo, decimal Preco);
}
