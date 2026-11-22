using Microsoft.AspNetCore.Identity;
using Locadora.Domain;

namespace Locadora.Infrastructure;

public sealed class UsuarioIdentity : IdentityUser<Guid>
{
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
}
