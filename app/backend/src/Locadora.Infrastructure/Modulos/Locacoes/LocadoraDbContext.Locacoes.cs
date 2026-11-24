using Locadora.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locadora.Infrastructure;

public sealed partial class LocadoraDbContext
{
    internal const string FiltroAluguelOculto = "AluguelOculto";
    public DbSet<Aluguel> Alugueis => Set<Aluguel>();
    public DbSet<ContratoSnapshot> ContratosSnapshot => Set<ContratoSnapshot>();
    public DbSet<ItemContratado> ItensContratados => Set<ItemContratado>();
    public DbSet<RegistroIdempotenciaAluguel> IdempotenciaAlugueis => Set<RegistroIdempotenciaAluguel>();
    public DbSet<FechamentoAluguel> FechamentosAlugueis => Set<FechamentoAluguel>();
    public DbSet<ItemFechamentoAluguel> ItensFechamentoAluguel => Set<ItemFechamentoAluguel>();

    internal IQueryable<Aluguel> AlugueisAbertos() => Alugueis.Where(x => x.Estado == EstadoAluguel.EmAberto);

    internal Task<bool> TaxaEmAluguelAbertoAsync(Guid taxaId, CancellationToken ct) =>
        AlugueisAbertos().AnyAsync(aluguel => aluguel.Revisoes.Any(revisao =>
            revisao.Numero == aluguel.RevisaoAtual && revisao.Itens.Any(x => x.TaxaId == taxaId)), ct);

    private void MapearLocacoes(ModelBuilder builder)
    {
        var aluguel = builder.Entity<Aluguel>();
        aluguel.ToTable("Alugueis", tabela =>
        {
            tabela.HasCheckConstraint("CK_Alugueis_AbertoNaoOcultado", "[Estado] <> 0 OR [ExcluidoEmUtc] IS NULL");
            tabela.HasCheckConstraint("CK_Alugueis_Estado", "[Estado] IN (0, 1)");
        });
        ConfigurarEntidadeEmpresa(aluguel, FiltroAluguelOculto);
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
        MapearFechamentos(builder, chave);
    }

    private void MapearFechamentos(ModelBuilder builder, EntityTypeBuilder<RegistroIdempotenciaAluguel> chave)
    {
        var final = builder.Entity<FechamentoAluguel>();
        final.ToTable("FechamentosAlugueis", tabela =>
        {
            tabela.HasCheckConstraint("CK_FechamentosAlugueis_Medicoes",
                "[Diarias] > 0 AND [HodometroInicial] >= 0 AND [HodometroFinal] >= [HodometroInicial] AND " +
                "[KmRodados] = [HodometroFinal] - [HodometroInicial] AND [KmCobrados] BETWEEN 0 AND [KmRodados] AND " +
                "[FranquiaTotalKm] >= 0 AND [CapacidadeLitros] > 0 AND [LitrosRestantes] BETWEEN 0 AND [CapacidadeLitros] AND " +
                "[LitrosARepor] = [CapacidadeLitros] - [LitrosRestantes] AND [InicioVigenciaCombustivelUtc] <= [RetornoRealUtc]");
            tabela.HasCheckConstraint("CK_FechamentosAlugueis_Valores",
                "[DiariasBruto] >= 0 AND [QuilometragemBruto] >= 0 AND [ValorPlano] >= 0 AND " +
                "[PrecoPorLitro] >= 0 AND [CombustivelBruto] >= 0 AND [ValorCombustivel] >= 0 AND " +
                "(([MultaBruto] IS NULL AND [ValorMulta] IS NULL) OR " +
                "([MultaBruto] IS NOT NULL AND [ValorMulta] IS NOT NULL AND [MultaBruto] >= 0 AND [ValorMulta] >= 0)) " +
                "AND [Total] >= 0 AND [Caucao] = 1000 AND [ExcluidoEmUtc] IS NULL");
        });
        ConfigurarEntidadeEmpresa(final);
        final.HasIndex(x => new { x.EmpresaId, x.AluguelId }).IsUnique();
        final.HasOne<ContratoSnapshot>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.AluguelId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.AluguelId, x.RevisaoId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<FechamentoAluguel, Aluguel>(final).HasForeignKey(x => new { x.EmpresaId, x.AluguelId })
            .OnDelete(DeleteBehavior.Restrict);
        Vinculo<FechamentoAluguel, PrecoCombustivel>(final).HasForeignKey(x => new { x.EmpresaId, x.PrecoCombustivelId })
            .OnDelete(DeleteBehavior.Restrict);
        final.Property(x => x.VersaoRegra).HasMaxLength(32).IsUnicode(false);
        final.Property(x => x.PrecoPorLitro).HasPrecision(19, 6);
        foreach (var nome in new[] { nameof(FechamentoAluguel.CapacidadeLitros), nameof(FechamentoAluguel.LitrosRestantes),
                     nameof(FechamentoAluguel.LitrosARepor) })
            final.Property(nome).HasPrecision(12, 3);
        foreach (var nome in new[] { nameof(FechamentoAluguel.DiariasBruto), nameof(FechamentoAluguel.QuilometragemBruto),
                     nameof(FechamentoAluguel.CombustivelBruto), nameof(FechamentoAluguel.MultaBruto) })
            final.Property(nome).HasPrecision(26, 9);
        foreach (var nome in new[] { nameof(FechamentoAluguel.ValorPlano), nameof(FechamentoAluguel.ValorCombustivel),
                     nameof(FechamentoAluguel.ValorMulta), nameof(FechamentoAluguel.Total), nameof(FechamentoAluguel.Caucao) })
            final.Property(nome).HasPrecision(19, 2);

        var item = builder.Entity<ItemFechamentoAluguel>();
        item.ToTable("ItensFechamentoAluguel", tabela => tabela.HasCheckConstraint("CK_ItensFechamentoAluguel_Origem",
            "(([Categoria] = 0 AND [TaxaId] IS NOT NULL AND [SeguroId] IS NULL AND [Modalidade] IN (0, 1)) OR " +
            "([Categoria] IN (1, 2) AND [TaxaId] IS NULL AND [SeguroId] IS NOT NULL AND [VersaoTaxaAdicional] IS NULL AND [Modalidade] = 1)) " +
            "AND [Quantidade] > 0 AND ([Modalidade] <> 0 OR [Quantidade] = 1) " +
            "AND [Preco] >= 0 AND [Bruto] >= 0 AND [Valor] >= 0 AND [ExcluidoEmUtc] IS NULL"));
        ConfigurarEntidadeEmpresa(item);
        item.HasOne<FechamentoAluguel>().WithMany(x => x.Adicionais).HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.FinalId }).OnDelete(DeleteBehavior.Restrict);
        Vinculo<ItemFechamentoAluguel, TaxaServico>(item).HasForeignKey(x => new { x.EmpresaId, x.TaxaId })
            .OnDelete(DeleteBehavior.Restrict);
        Vinculo<ItemFechamentoAluguel, SeguroConfiguracao>(item).HasForeignKey(x => new { x.EmpresaId, x.SeguroId })
            .OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(x => new { x.EmpresaId, x.FinalId, x.TaxaId }).IsUnique().HasFilter("[TaxaId] IS NOT NULL");
        item.HasIndex(x => new { x.EmpresaId, x.FinalId, x.Categoria }).IsUnique().HasFilter("[SeguroId] IS NOT NULL");
        item.Property(x => x.Nome).HasMaxLength(200);
        item.Property(x => x.Preco).HasPrecision(19, 6);
        item.Property(x => x.Bruto).HasPrecision(26, 9);
        item.Property(x => x.Valor).HasPrecision(19, 2);
        item.Property(x => x.VersaoTaxaAdicional).HasMaxLength(8).IsFixedLength();

        chave.ToTable("IdempotenciaAlugueis", tabela => tabela.HasCheckConstraint("CK_IdempotenciaAlugueis_Resultado",
            "([Operacao] = 'criar' AND [FinalId] IS NULL) OR ([Operacao] = 'devolver' AND [FinalId] IS NOT NULL)"));
        chave.HasOne<FechamentoAluguel>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.Id, x.RevisaoId })
            .HasForeignKey(x => new { x.EmpresaId, x.FinalId, x.RevisaoId }).OnDelete(DeleteBehavior.Restrict);
    }

    private static ReferenceCollectionBuilder<TPrincipal, TDependente> Vinculo<TDependente, TPrincipal>(
        EntityTypeBuilder<TDependente> entidade) where TDependente : EntidadeEmpresa where TPrincipal : EntidadeEmpresa
        => entidade.HasOne<TPrincipal>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.Id });
}
