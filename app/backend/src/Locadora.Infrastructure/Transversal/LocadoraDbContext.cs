using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Locadora.Domain;
using Locadora.Application;

namespace Locadora.Infrastructure;

public sealed class LocadoraDbContext(DbContextOptions<LocadoraDbContext> options, IContextoEmpresa contexto)
    : IdentityDbContext<UsuarioIdentity, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sessao> Sessoes => Set<Sessao>();
    public DbSet<RefreshTokenRegistro> RefreshTokens => Set<RefreshTokenRegistro>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<RecursoTenantTeste> RecursosTenantTeste => Set<RecursoTenantTeste>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Empresa>().ToTable("Empresas");
        builder.Entity<Empresa>().HasKey(x => x.Id);
        builder.Entity<UsuarioIdentity>().ToTable("Usuarios");
        builder.Entity<UsuarioIdentity>().HasIndex(x => x.NormalizedUserName).IsUnique();
        builder.Entity<UsuarioIdentity>().HasIndex(x => x.EmpresaId).IsUnique();
        builder.Entity<UsuarioIdentity>().HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Entity<UsuarioIdentity>().HasOne(x => x.Empresa).WithMany()
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Sessao>().ToTable("Sessoes");
        builder.Entity<Sessao>().HasKey(x => x.Id);
        builder.Entity<Sessao>().Property(x => x.VersaoAcesso).HasMaxLength(256);
        builder.Entity<Sessao>().HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Entity<Sessao>().HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Sessao>().HasIndex(x => x.RefreshHash).IsUnique();
        builder.Entity<Sessao>().HasOne<Empresa>().WithMany(x => x.Sessoes)
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RefreshTokenRegistro>().ToTable("RefreshTokens");
        builder.Entity<RefreshTokenRegistro>().HasKey(x => x.Id);
        builder.Entity<RefreshTokenRegistro>().HasIndex(x => x.Hash).IsUnique();
        builder.Entity<RefreshTokenRegistro>().HasOne<Sessao>().WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => new { x.EmpresaId, x.SessaoId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Auditoria>().ToTable("Auditorias");
        builder.Entity<Auditoria>().HasKey(x => x.Id);
        builder.Entity<RecursoTenantTeste>().ToTable("RecursosTenantTeste");
        builder.Entity<RecursoTenantTeste>().HasKey(x => x.Id);
        // Inclui o tenant no WHERE de update/delete, inclusive para entidades anexadas sem leitura prévia.
        builder.Entity<RecursoTenantTeste>().Property(x => x.EmpresaId).IsConcurrencyToken();
        builder.Entity<RecursoTenantTeste>().HasIndex(x => x.EmpresaId);
        builder.Entity<RecursoTenantTeste>().HasAlternateKey(x => new { x.EmpresaId, x.Id });
        builder.Entity<RecursoTenantTeste>().HasQueryFilter(x => x.EmpresaId == contexto.EmpresaId);
        builder.Entity<RecursoTenantTeste>().HasOne<Empresa>().WithMany()
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RecursoTenantTeste>().HasOne<RecursoTenantTeste>().WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.ReferenciaId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidarEscritas();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarEscritas();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ValidarEscritas()
    {
        foreach (var entrada in ChangeTracker.Entries<RecursoTenantTeste>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (contexto.EmpresaId is not Guid empresa || entrada.Entity.EmpresaId != empresa ||
                (entrada.State != EntityState.Added && entrada.Property(x => x.EmpresaId).OriginalValue != empresa))
                throw new UnauthorizedAccessException("Escrita fora do contexto autorizado.");
        }
    }
}
