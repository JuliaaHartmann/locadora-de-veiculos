namespace Locadora.Domain;

public sealed class RefreshTokenRegistro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessaoId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid FamiliaId { get; set; }
    public required byte[] Hash { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConsumidoEm { get; set; }
    public DateTimeOffset? RevogadoEm { get; set; }
}
