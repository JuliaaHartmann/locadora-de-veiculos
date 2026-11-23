using Locadora.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locadora.Infrastructure;

public sealed partial class LocadoraDbContext
{
    public DbSet<Aluguel> Alugueis => Set<Aluguel>();
    public DbSet<ContratoSnapshot> ContratosSnapshot => Set<ContratoSnapshot>();
    public DbSet<ItemContratado> ItensContratados => Set<ItemContratado>();
    public DbSet<RegistroIdempotenciaAluguel> IdempotenciaAlugueis => Set<RegistroIdempotenciaAluguel>();

    internal IQueryable<Aluguel> AlugueisAbertos() => Alugueis.Where(x => x.Estado == EstadoAluguel.EmAberto);

    internal Task<bool> TaxaEmAluguelAbertoAsync(Guid taxaId, CancellationToken ct) =>
        AlugueisAbertos().AnyAsync(aluguel => aluguel.Revisoes.Any(revisao =>
            revisao.Numero == aluguel.RevisaoAtual && revisao.Itens.Any(x => x.TaxaId == taxaId)), ct);

    private void MapearLocacoes(ModelBuilder builder)
    {
        var aluguel = builder.Entity<Aluguel>();
        aluguel.ToTable("Alugueis", tabela => tabela.HasCheckConstraint("CK_Alugueis_AbertoNaoOcultado",
            "[Estado] <> 0 OR [ExcluidoEmUtc] IS NULL"));
        ConfigurarEntidadeEmpresa(aluguel);
        aluguel.HasIndex(x => new { x.EmpresaId, x.VeiculoId }).IsUnique().HasFilter("[Estado] = 0");
        aluguel.HasIndex(x => new { x.EmpresaId, x.Estado, x.SaidaUtc, x.Id });
        Vinculo<Aluguel, Cliente>(aluguel).HasForeignKey(x => new { x.EmpresaId, x.ClienteId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<Aluguel, Condutor>(aluguel).HasForeignKey(x => new { x.EmpresaId, x.CondutorId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<Aluguel, Veiculo>(aluguel).HasForeignKey(x => new { x.EmpresaId, x.VeiculoId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<Aluguel, Plano>(aluguel).HasForeignKey(x => new { x.EmpresaId, x.PlanoId }).OnDelete(DeleteBehavior.Restrict);

        var revisao = builder.Entity<ContratoSnapshot>();
        revisao.ToTable("ContratosSnapshot");
        ConfigurarEntidadeEmpresa(revisao);
        revisao.HasIndex(x => new { x.EmpresaId, x.AluguelId, x.Numero }).IsUnique();
        revisao.HasOne<Aluguel>().WithMany(x => x.Revisoes).HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.AluguelId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ContratoSnapshot, Cliente>(revisao).HasForeignKey(x => new { x.EmpresaId, x.ClienteId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ContratoSnapshot, Condutor>(revisao).HasForeignKey(x => new { x.EmpresaId, x.CondutorId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ContratoSnapshot, Veiculo>(revisao).HasForeignKey(x => new { x.EmpresaId, x.VeiculoId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ContratoSnapshot, Grupo>(revisao).HasForeignKey(x => new { x.EmpresaId, x.GrupoId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ContratoSnapshot, Plano>(revisao).HasForeignKey(x => new { x.EmpresaId, x.PlanoId }).OnDelete(DeleteBehavior.Restrict);
        revisao.Property(x => x.ClienteNome).HasMaxLength(200);
        revisao.Property(x => x.ClienteDocumento).HasMaxLength(14);
        revisao.Property(x => x.CondutorNome).HasMaxLength(200);
        revisao.Property(x => x.CondutorCpf).HasMaxLength(11);
        revisao.Property(x => x.CondutorCnh).HasMaxLength(32);
        revisao.Property(x => x.VeiculoPlaca).HasMaxLength(16);
        revisao.Property(x => x.VeiculoMarca).HasMaxLength(200);
        revisao.Property(x => x.VeiculoModelo).HasMaxLength(200);
        revisao.Property(x => x.GrupoNome).HasMaxLength(200);
        revisao.Property(x => x.CapacidadeLitros).HasPrecision(12, 3);
        revisao.Property(x => x.PrecoDiario).HasPrecision(19, 6);
        revisao.Property(x => x.PrecoKm).HasPrecision(19, 6);
        revisao.Property(x => x.ValorPlano).HasPrecision(28, 2);
        revisao.Property(x => x.ParcelasConhecidas).HasPrecision(28, 2);
        revisao.Property(x => x.Caucao).HasPrecision(19, 2);

        var item = builder.Entity<ItemContratado>();
        item.ToTable("ItensContratados", tabela => tabela.HasCheckConstraint("CK_ItensContratados_Origem",
            "([Categoria] = 0 AND [TaxaId] IS NOT NULL AND [SeguroId] IS NULL) OR " +
            "([Categoria] IN (1, 2) AND [TaxaId] IS NULL AND [SeguroId] IS NOT NULL AND [Modalidade] = 1)"));
        ConfigurarEntidadeEmpresa(item);
        item.HasOne<ContratoSnapshot>().WithMany(x => x.Itens).HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.RevisaoId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ItemContratado, TaxaServico>(item).HasForeignKey(x => new { x.EmpresaId, x.TaxaId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ItemContratado, SeguroConfiguracao>(item).HasForeignKey(x => new { x.EmpresaId, x.SeguroId }).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(x => new { x.EmpresaId, x.RevisaoId, x.TaxaId }).IsUnique().HasFilter("[TaxaId] IS NOT NULL");
        item.HasIndex(x => new { x.EmpresaId, x.RevisaoId, x.Categoria }).IsUnique().HasFilter("[SeguroId] IS NOT NULL");
        item.Property(x => x.Nome).HasMaxLength(200);
        item.Property(x => x.Preco).HasPrecision(19, 6);
        item.Property(x => x.Valor).HasPrecision(28, 2);

        var chave = builder.Entity<RegistroIdempotenciaAluguel>();
        chave.ToTable("IdempotenciaAlugueis");
        ConfigurarEntidadeEmpresa(chave);
        chave.Property(x => x.Operacao).HasMaxLength(20).IsUnicode(false);
        chave.Property(x => x.Chave).HasMaxLength(128).UseCollation("Latin1_General_100_BIN2").IsUnicode(false);
        chave.Property(x => x.HashComando).HasMaxLength(32).IsFixedLength();
        chave.Property(x => x.VersaoOriginal).HasMaxLength(8).IsFixedLength();
        chave.HasIndex(x => new { x.EmpresaId, x.UsuarioId, x.Operacao, x.Chave }).IsUnique();
        Vinculo<RegistroIdempotenciaAluguel, ContratoSnapshot>(chave)
            .HasForeignKey(x => new { x.EmpresaId, x.RevisaoId }).OnDelete(DeleteBehavior.Restrict);
        chave.HasOne<UsuarioIdentity>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId }).OnDelete(DeleteBehavior.Restrict);
    }

    private static ReferenceCollectionBuilder<TPrincipal, TDependente> Vinculo<TDependente, TPrincipal>(
        EntityTypeBuilder<TDependente> entidade) where TDependente : EntidadeEmpresa where TPrincipal : EntidadeEmpresa
        => entidade.HasOne<TPrincipal>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.Id });
}
