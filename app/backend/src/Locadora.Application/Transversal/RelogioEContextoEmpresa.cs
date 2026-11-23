namespace Locadora.Application;

public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public interface IContextoEmpresa
{
    Guid? EmpresaId { get; }
    Guid? UsuarioId { get; }
    void Definir(Guid empresaId, Guid? usuarioId = null);
}
