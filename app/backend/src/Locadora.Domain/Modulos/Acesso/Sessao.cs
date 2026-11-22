namespace Locadora.Domain;

public sealed class Sessao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string VersaoAcesso { get; set; } = "";
    public DateTimeOffset CriadaEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiraEm { get; set; }
    public DateTimeOffset? RevogadaEm { get; set; }
    public Guid FamiliaRefreshId { get; set; } = Guid.NewGuid();
    public byte[]? RefreshHash { get; set; }
    public DateTimeOffset? RefreshConsumidoEm { get; set; }
    public ICollection<RefreshTokenRegistro> RefreshTokens { get; set; } = new List<RefreshTokenRegistro>();
}
