using Locadora.Domain;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure;

public sealed partial class LocadoraDbContext
{
    public DbSet<FotoObjeto> FotosObjetos => Set<FotoObjeto>();

    private void MapearFotos(ModelBuilder builder)
    {
        var foto = builder.Entity<FotoObjeto>();
        foto.ToTable("FotosObjetos", tabela =>
        {
            tabela.HasCheckConstraint("CK_FotosObjetos_Estado", "[Estado] BETWEEN 0 AND 5 AND [ExcluidoEmUtc] IS NULL");
            tabela.HasCheckConstraint("CK_FotosObjetos_Conteudo", "[Tamanho] > 0 AND [Mime] IN ('image/jpeg', 'image/png') AND [TentativasCompensacao] >= 0");
        });
        ConfigurarEntidadeEmpresa(foto);
        foto.HasOne<Veiculo>().WithMany().HasPrincipalKey(x => new { x.EmpresaId, x.Id })
            .HasForeignKey(x => new { x.EmpresaId, x.VeiculoId }).OnDelete(DeleteBehavior.Restrict);
        foto.HasIndex(x => new { x.EmpresaId, x.VeiculoId }).IsUnique().HasFilter("[Estado] = 1");
        foto.HasIndex(x => new { x.EmpresaId, x.Deposito, x.Estado, x.RetomarAposUtc });
        foto.HasIndex(x => new { x.Deposito, x.Chave }).IsUnique();
        foto.Property(x => x.UrlRelativa).HasMaxLength(100).IsUnicode(false);
        foto.Property(x => x.Deposito).HasMaxLength(300).IsUnicode(false);
        foto.Property(x => x.Chave).HasMaxLength(80).IsUnicode(false);
        foto.Property(x => x.Mime).HasMaxLength(16).IsUnicode(false);
        foto.Property(x => x.Hash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
    }
}
