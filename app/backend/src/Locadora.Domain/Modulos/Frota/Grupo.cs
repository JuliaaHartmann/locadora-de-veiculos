namespace Locadora.Domain;

public sealed class Grupo : EntidadeNumerada
{
    public string Nome { get; set; } = string.Empty;
    public ICollection<Plano> Planos { get; set; } = new List<Plano>();
    public ICollection<Veiculo> Veiculos { get; set; } = new List<Veiculo>();

    public static Grupo Criar(string? nome, Guid empresaId, DateTimeOffset agora)
    {
        var normalizado = (nome ?? string.Empty).Trim();
        ValidarNome(normalizado);
        return new Grupo { Nome = normalizado, EmpresaId = empresaId, CriadoEm = agora, AtualizadoEm = agora };
    }

    public void Renomear(string? nome, DateTimeOffset agora)
    {
        var normalizado = (nome ?? string.Empty).Trim();
        ValidarNome(normalizado);
        Nome = normalizado;
        AtualizadoEm = agora;
    }

    private static void ValidarNome(string nome)
    {
        var erros = new List<ErroValidacao>();
        Validador.Adicionar(erros, "nome", Validador.Obrigatorio(nome, 200));
        Validador.Lancar(erros);
    }
}
