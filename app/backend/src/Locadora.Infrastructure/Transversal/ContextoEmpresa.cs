using Locadora.Application;

namespace Locadora.Infrastructure;

public sealed class ContextoEmpresa : IContextoEmpresa
{
    public Guid? EmpresaId { get; private set; }
    public Guid? UsuarioId { get; private set; }

    public void Definir(Guid empresaId, Guid? usuarioId = null)
    {
        EmpresaId = empresaId;
        UsuarioId = usuarioId;
    }
}
