namespace Locadora.Application;

public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public interface IContextoEmpresa
{
    Guid? EmpresaId { get; }
    void Definir(Guid empresaId);
}
