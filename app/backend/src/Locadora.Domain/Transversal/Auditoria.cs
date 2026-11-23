namespace Locadora.Domain;

public sealed class Auditoria
{
    public long Id { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public required string Evento { get; set; }
    public string? EntidadeTipo { get; set; }
    public Guid? EntidadeId { get; set; }
    public Guid? CorrelacaoId { get; set; }
    public string? CamposAlterados { get; set; }
    public DateTimeOffset OcorridoEm { get; set; } = DateTimeOffset.UtcNow;
}
