using System.Data;
using Locadora.Application;
using Locadora.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace Locadora.Infrastructure;

public sealed partial class LocadoraDbContext
{
    private sealed class ContadorCodigo
    {
        public Guid EmpresaId { get; set; }
        public byte TipoRecurso { get; set; }
        public int UltimoNumero { get; set; }
    }

    private static void MapearCodigos(ModelBuilder builder)
    {
        builder.Entity<ContadorCodigo>(entidade =>
        {
            entidade.ToTable("ContadoresCodigo", tabela =>
            {
                tabela.HasCheckConstraint("CK_ContadoresCodigo_Tipo", "[TipoRecurso] BETWEEN 1 AND 8");
                tabela.HasCheckConstraint("CK_ContadoresCodigo_Numero", "[UltimoNumero] BETWEEN 1 AND 999999");
            });
            entidade.HasKey(x => new { x.EmpresaId, x.TipoRecurso });
            entidade.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigurarCodigo(builder.Entity<Grupo>());
        ConfigurarCodigo(builder.Entity<Plano>());
        ConfigurarCodigo(builder.Entity<Veiculo>());
        ConfigurarCodigo(builder.Entity<Cliente>());
        ConfigurarCodigo(builder.Entity<Condutor>());
        ConfigurarCodigo(builder.Entity<TaxaServico>());
        ConfigurarCodigo(builder.Entity<PrecoCombustivel>());
        ConfigurarCodigo(builder.Entity<Aluguel>());
    }

    private static void ConfigurarCodigo<T>(EntityTypeBuilder<T> entidade) where T : EntidadeNumerada
    {
        entidade.ToTable(tabela => tabela.HasCheckConstraint($"CK_{entidade.Metadata.GetTableName()}_NumeroExibicao",
            "[NumeroExibicao] BETWEEN 1 AND 999999"));
        entidade.HasIndex(x => new { x.EmpresaId, x.NumeroExibicao }).IsUnique();
    }

    private EntidadeNumerada[] NovasEntidadesNumeradas() => ChangeTracker.Entries<EntidadeNumerada>()
        .Where(x => x.State == EntityState.Added).Select(x => x.Entity).OrderBy(TipoDoRecurso).ToArray();

    // Estes valores persistidos coincidem com o preenchimento da migration; não renumerar.
    private static byte TipoDoRecurso(EntidadeNumerada entidade) => entidade switch
    {
        Grupo => 1, Plano => 2, Veiculo => 3, Cliente => 4, Condutor => 5,
        TaxaServico => 6, PrecoCombustivel => 7, Aluguel => 8,
        _ => throw new InvalidOperationException("Tipo de recurso sem sequência de apresentação."),
    };

    private async Task<int> ReservarCodigoAsync(EntidadeNumerada entidade, CancellationToken ct)
    {
        using var comando = Database.GetDbConnection().CreateCommand();
        comando.Transaction = Database.CurrentTransaction!.GetDbTransaction();
        comando.CommandTimeout = Database.GetCommandTimeout() ?? comando.CommandTimeout;
        comando.CommandText = """
            DECLARE @ultimo int;
            SELECT @ultimo = UltimoNumero FROM dbo.ContadoresCodigo WITH (UPDLOCK, HOLDLOCK)
                WHERE EmpresaId = @empresa AND TipoRecurso = @tipo;
            IF @ultimo IS NULL
            BEGIN
                SET @ultimo = 1;
                INSERT dbo.ContadoresCodigo (EmpresaId, TipoRecurso, UltimoNumero) VALUES (@empresa, @tipo, @ultimo);
            END
            ELSE IF @ultimo < 999999
            BEGIN
                SET @ultimo += 1;
                UPDATE dbo.ContadoresCodigo SET UltimoNumero = @ultimo WHERE EmpresaId = @empresa AND TipoRecurso = @tipo;
            END
            ELSE
                SET @ultimo = 0;
            SELECT @ultimo;
            """;
        var empresa = comando.CreateParameter();
        empresa.ParameterName = "@empresa"; empresa.DbType = DbType.Guid; empresa.Value = entidade.EmpresaId;
        comando.Parameters.Add(empresa);
        var tipo = comando.CreateParameter();
        tipo.ParameterName = "@tipo"; tipo.DbType = DbType.Byte; tipo.Value = TipoDoRecurso(entidade);
        comando.Parameters.Add(tipo);

        var numero = (int)(await comando.ExecuteScalarAsync(ct))!;
        if (numero == 0)
            throw new ConflitoException("O limite de códigos para este tipo de registro foi atingido nesta empresa.");
        return numero;
    }
}
