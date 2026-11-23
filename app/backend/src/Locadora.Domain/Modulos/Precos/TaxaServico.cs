namespace Locadora.Domain;

public enum ModalidadeTaxa
{
    Fixa,
    Diaria,
}

public sealed class TaxaServico : EntidadeEmpresa
{
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public ModalidadeTaxa Modalidade { get; set; }

    public static TaxaServico Criar(string? nome, decimal? preco, ModalidadeTaxa modalidade,
        Guid empresaId, DateTimeOffset agora)
    {
        var dados = Normalizar(nome, preco, modalidade);
        return new TaxaServico
        {
            Nome = dados.Nome,
            Preco = dados.Preco,
            Modalidade = dados.Modalidade,
            EmpresaId = empresaId,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void Atualizar(string? nome, decimal? preco, ModalidadeTaxa modalidade, DateTimeOffset agora)
    {
        var dados = Normalizar(nome, preco, modalidade);
        Nome = dados.Nome;
        Preco = dados.Preco;
        Modalidade = dados.Modalidade;
        AtualizadoEm = agora;
    }

    private static Dados Normalizar(string? nome, decimal? preco, ModalidadeTaxa modalidade)
    {
        var erros = new List<ErroValidacao>();
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        Validador.Adicionar(erros, "nome", Validador.Obrigatorio(nomeNormalizado, 200));
        Validador.Adicionar(erros, "preco", Validador.DecimalNaoNegativo(preco));
        if (!Enum.IsDefined(modalidade))
            erros.Add(new ErroValidacao("modalidade", "Selecione a modalidade fixa ou diária."));
        Validador.Lancar(erros);
        return new Dados(nomeNormalizado, preco!.Value, modalidade);
    }

    private sealed record Dados(string Nome, decimal Preco, ModalidadeTaxa Modalidade);
}
