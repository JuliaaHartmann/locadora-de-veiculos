namespace Locadora.Domain;

public enum EstadoFoto { Pendente, Ativa, Retida, Orfa, Removida, FalhaCompensacao }

public sealed class FotoObjeto : EntidadeEmpresa
{
    public Guid VeiculoId { get; init; }
    public required string UrlRelativa { get; init; }
    public required string Deposito { get; init; }
    public required string Chave { get; init; }
    public required string Mime { get; init; }
    public long Tamanho { get; init; }
    public required string Hash { get; init; }
    public EstadoFoto Estado { get; set; }
    public DateTimeOffset RetomarAposUtc { get; set; }
    public int TentativasCompensacao { get; set; }
}
