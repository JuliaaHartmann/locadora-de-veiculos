using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Locadora.Infrastructure;

public sealed class LocadoraDbContextFactory : IDesignTimeDbContextFactory<LocadoraDbContext>
{
    public LocadoraDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Locadora")
            ?? throw new InvalidOperationException("Forneça ConnectionStrings__Locadora no processo de design.");
        return new(new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(conexao).Options, new ContextoEmpresa());
    }
}
