using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Locadora.Domain;
using Locadora.Application;

namespace Locadora.Infrastructure;

public sealed partial class LocadoraDbContext(DbContextOptions<LocadoraDbContext> options, IContextoEmpresa contexto)
    : IdentityDbContext<UsuarioIdentity, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sessao> Sessoes => Set<Sessao>();
    public DbSet<RefreshTokenRegistro> RefreshTokens => Set<RefreshTokenRegistro>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<RecursoTenantTeste> RecursosTenantTeste => Set<RecursoTenantTeste>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Plano> Planos => Set<Plano>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Condutor> Condutores => Set<Condutor>();
    public DbSet<VinculoPfPj> Vinculos => Set<VinculoPfPj>();
    public DbSet<TaxaServico> TaxasServico => Set<TaxaServico>();
    public DbSet<SeguroConfiguracao> Seguros => Set<SeguroConfiguracao>();
    public DbSet<PrecoCombustivel> PrecosCombustivel => Set<PrecoCombustivel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        MapearAcesso(builder);
        MapearFrota(builder);
        MapearPessoas(builder);
        MapearPrecos(builder);
        MapearLocacoes(builder);
        MapearFotos(builder);
        MapearAuditoria(builder);
    }

    private void MapearAcesso(ModelBuilder builder)
    {
        builder.Entity<Empresa>().ToTable("Empresas");
        builder.Entity<Empresa>().HasKey(x => x.Id);
        builder.Entity<Empresa>().Property(x => x.Versao).IsRowVersion();
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

    private void MapearFrota(ModelBuilder builder)
    {
        builder.Entity<Grupo>().ToTable("Grupos");
        ConfigurarEntidadeEmpresa(builder.Entity<Grupo>());
        builder.Entity<Grupo>().Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Entity<Grupo>().HasIndex(x => new { x.EmpresaId, x.Nome });

        builder.Entity<Plano>().ToTable("Planos");
        ConfigurarEntidadeEmpresa(builder.Entity<Plano>());
        builder.Entity<Plano>().Property(x => x.PrecoDiario).HasPrecision(19, 6);
        builder.Entity<Plano>().Property(x => x.PrecoKm).HasPrecision(19, 6);
        builder.Entity<Plano>().HasOne(x => x.Grupo).WithMany(x => x.Planos)
            .HasForeignKey(x => new { x.EmpresaId, x.GrupoId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Plano>().HasIndex(x => new { x.EmpresaId, x.GrupoId, x.Modalidade })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");

        builder.Entity<Veiculo>().ToTable("Veiculos");
        ConfigurarEntidadeEmpresa(builder.Entity<Veiculo>());
        builder.Entity<Veiculo>().Property(x => x.Placa).HasMaxLength(16).IsRequired();
        builder.Entity<Veiculo>().Property(x => x.Marca).HasMaxLength(200).IsRequired();
        builder.Entity<Veiculo>().Property(x => x.Modelo).HasMaxLength(200).IsRequired();
        builder.Entity<Veiculo>().Property(x => x.Cor).HasMaxLength(100).IsRequired();
        builder.Entity<Veiculo>().Property(x => x.CapacidadeLitros).HasPrecision(12, 3);
        builder.Entity<Veiculo>().HasOne(x => x.Grupo).WithMany(x => x.Veiculos)
            .HasForeignKey(x => new { x.EmpresaId, x.GrupoId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Veiculo>().HasIndex(x => new { x.EmpresaId, x.Placa })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        builder.Entity<Veiculo>().HasIndex(x => new { x.EmpresaId, x.GrupoId });
    }

    private void MapearPessoas(ModelBuilder builder)
    {
        builder.Entity<Cliente>().ToTable("Clientes");
        ConfigurarEntidadeEmpresa(builder.Entity<Cliente>());
        builder.Entity<Cliente>().Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Entity<Cliente>().Property(x => x.Cpf).HasMaxLength(11);
        builder.Entity<Cliente>().Property(x => x.Rg).HasMaxLength(32);
        builder.Entity<Cliente>().Property(x => x.Cnh).HasMaxLength(32);
        builder.Entity<Cliente>().Property(x => x.Cnpj).HasMaxLength(14);
        builder.Entity<Cliente>().Property(x => x.Telefone).HasMaxLength(32).IsRequired();
        builder.Entity<Cliente>().Property(x => x.Email).HasMaxLength(254);
        builder.Entity<Cliente>().OwnsOne(x => x.Endereco, endereco =>
        {
            endereco.Property(x => x.Estado).HasColumnName("EnderecoEstado").HasMaxLength(100).IsRequired();
            endereco.Property(x => x.Cidade).HasColumnName("EnderecoCidade").HasMaxLength(200).IsRequired();
            endereco.Property(x => x.Bairro).HasColumnName("EnderecoBairro").HasMaxLength(200).IsRequired();
            endereco.Property(x => x.Rua).HasColumnName("EnderecoRua").HasMaxLength(200).IsRequired();
            endereco.Property(x => x.Numero).HasColumnName("EnderecoNumero").HasMaxLength(40).IsRequired();
        });
        builder.Entity<Cliente>().HasIndex(x => new { x.EmpresaId, x.Nome });
        builder.Entity<Cliente>().HasIndex(x => new { x.EmpresaId, x.Cpf })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL AND [Tipo] = 0");
        builder.Entity<Cliente>().HasIndex(x => new { x.EmpresaId, x.Cnpj })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL AND [Tipo] = 1");

        builder.Entity<Condutor>().ToTable("Condutores");
        ConfigurarEntidadeEmpresa(builder.Entity<Condutor>());
        builder.Entity<Condutor>().Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Entity<Condutor>().Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Entity<Condutor>().Property(x => x.Cpf).HasMaxLength(11).IsRequired();
        builder.Entity<Condutor>().Property(x => x.Cnh).HasMaxLength(32).IsRequired();
        builder.Entity<Condutor>().Property(x => x.Telefone).HasMaxLength(32).IsRequired();
        builder.Entity<Condutor>().HasOne(x => x.Cliente).WithMany()
            .HasForeignKey(x => new { x.EmpresaId, x.ClienteId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Condutor>().HasIndex(x => new { x.EmpresaId, x.Cpf })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        builder.Entity<Condutor>().HasIndex(x => new { x.EmpresaId, x.ClienteId });

        builder.Entity<VinculoPfPj>().ToTable("VinculosPfPj");
        builder.Entity<VinculoPfPj>().HasKey(x => x.Id);
        builder.Entity<VinculoPfPj>().Property(x => x.EmpresaId).IsConcurrencyToken();
        builder.Entity<VinculoPfPj>().HasIndex(x => x.EmpresaId);
        builder.Entity<VinculoPfPj>().HasQueryFilter(x => x.EmpresaId == contexto.EmpresaId);
        builder.Entity<VinculoPfPj>().HasOne(x => x.ClientePf).WithMany(x => x.VinculosComoPf)
            .HasForeignKey(x => new { x.EmpresaId, x.ClientePfId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<VinculoPfPj>().HasOne(x => x.ClientePj).WithMany(x => x.VinculosComoPj)
            .HasForeignKey(x => new { x.EmpresaId, x.ClientePjId })
            .HasPrincipalKey(x => new { x.EmpresaId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<VinculoPfPj>().HasIndex(x => new { x.EmpresaId, x.ClientePfId, x.ClientePjId })
            .IsUnique();
    }

    private void MapearPrecos(ModelBuilder builder)
    {
        builder.Entity<TaxaServico>().ToTable("TaxasServico");
        ConfigurarEntidadeEmpresa(builder.Entity<TaxaServico>());
        builder.Entity<TaxaServico>().Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Entity<TaxaServico>().Property(x => x.Preco).HasPrecision(19, 6);
        builder.Entity<TaxaServico>().HasIndex(x => new { x.EmpresaId, x.Nome });

        builder.Entity<SeguroConfiguracao>().ToTable("Seguros");
        ConfigurarEntidadeEmpresa(builder.Entity<SeguroConfiguracao>());
        builder.Entity<SeguroConfiguracao>().Property(x => x.PrecoDiario).HasPrecision(19, 6);
        builder.Entity<SeguroConfiguracao>().HasIndex(x => new { x.EmpresaId, x.Tipo }).IsUnique();

        builder.Entity<PrecoCombustivel>().ToTable("PrecosCombustivel");
        ConfigurarEntidadeEmpresa(builder.Entity<PrecoCombustivel>());
        builder.Entity<PrecoCombustivel>().Property(x => x.Preco).HasPrecision(19, 6);
        builder.Entity<PrecoCombustivel>().HasIndex(x => new
            { x.EmpresaId, x.Tipo, x.InicioVigenciaUtc, x.Ordem, x.Id });
    }

    private void MapearAuditoria(ModelBuilder builder)
    {
        builder.Entity<Auditoria>().ToTable("Auditorias");
        builder.Entity<Auditoria>().HasKey(x => x.Id);
        builder.Entity<Auditoria>().Property(x => x.Evento).IsRequired();
        builder.Entity<Auditoria>().Property(x => x.EntidadeTipo).HasMaxLength(100);
        builder.Entity<Auditoria>().Property(x => x.CamposAlterados).HasMaxLength(500);
        builder.Entity<Auditoria>().HasIndex(x => new { x.EmpresaId, x.OcorridoEm });
    }

    private void ConfigurarEntidadeEmpresa<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entidade,
        string? filtroOcultacao = null)
        where T : EntidadeEmpresa
    {
        entidade.HasKey(x => x.Id);
        entidade.Property(x => x.EmpresaId).IsConcurrencyToken();
        entidade.Property(x => x.Versao).IsRowVersion();
        entidade.HasAlternateKey(x => new { x.EmpresaId, x.Id });
        if (filtroOcultacao is null)
            entidade.HasQueryFilter(x => x.EmpresaId == contexto.EmpresaId && x.ExcluidoEmUtc == null);
        else
        {
            entidade.HasQueryFilter("Empresa", x => x.EmpresaId == contexto.EmpresaId);
            entidade.HasQueryFilter(filtroOcultacao, x => x.ExcluidoEmUtc == null);
        }
        entidade.HasOne<Empresa>().WithMany()
            .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
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

        foreach (var entrada in ChangeTracker.Entries<EntidadeEmpresa>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (contexto.EmpresaId is not Guid empresa || entrada.Entity.EmpresaId != empresa ||
                (entrada.State != EntityState.Added && entrada.Property(x => x.EmpresaId).OriginalValue != empresa))
                throw new UnauthorizedAccessException("Escrita fora do contexto autorizado.");
            if (entrada.State != EntityState.Added && entrada.Entity is FechamentoAluguel or ItemFechamentoAluguel)
                throw new InvalidOperationException("O demonstrativo final é imutável.");
        }

        foreach (var entrada in ChangeTracker.Entries<VinculoPfPj>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (contexto.EmpresaId is not Guid empresa || entrada.Entity.EmpresaId != empresa ||
                (entrada.State != EntityState.Added && entrada.Property(x => x.EmpresaId).OriginalValue != empresa))
                throw new UnauthorizedAccessException("Escrita fora do contexto autorizado.");
        }
    }
}
