namespace Locadora.Domain;

public sealed class Empresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CriadaEm { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Sessao> Sessoes { get; set; } = new List<Sessao>();
}
