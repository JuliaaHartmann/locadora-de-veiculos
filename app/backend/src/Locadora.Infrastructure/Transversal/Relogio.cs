using Locadora.Application;

namespace Locadora.Infrastructure;

public sealed class Relogio(TimeProvider tempo) : IRelogio
{
    public DateTimeOffset Agora => tempo.GetUtcNow();
}
