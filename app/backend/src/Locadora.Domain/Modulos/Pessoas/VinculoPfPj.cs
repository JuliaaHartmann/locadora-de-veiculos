namespace Locadora.Domain;

public sealed class VinculoPfPj
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmpresaId { get; set; }
    public Guid ClientePfId { get; set; }
    public Cliente? ClientePf { get; set; }
    public Guid ClientePjId { get; set; }
    public Cliente? ClientePj { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
}
