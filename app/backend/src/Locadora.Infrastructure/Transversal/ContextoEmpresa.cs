using Locadora.Application;

namespace Locadora.Infrastructure;

public sealed class ContextoEmpresa : IContextoEmpresa
{
    public Guid? EmpresaId { get; private set; }

    public void Definir(Guid empresaId) => EmpresaId = empresaId;
}
