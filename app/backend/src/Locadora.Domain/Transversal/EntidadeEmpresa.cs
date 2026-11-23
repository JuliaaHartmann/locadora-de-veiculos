namespace Locadora.Domain;

/// <summary>Base das entidades isoladas por empresa, com exclusão lógica e concorrência otimista.</summary>
public abstract class EntidadeEmpresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmpresaId { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    public DateTimeOffset? ExcluidoEmUtc { get; set; }
    public byte[] Versao { get; set; } = [];
}
