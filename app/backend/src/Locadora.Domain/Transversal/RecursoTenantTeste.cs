namespace Locadora.Domain;

public sealed class RecursoTenantTeste
{
    public Guid? ReferenciaId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmpresaId { get; set; }
    public required string Valor { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
}
