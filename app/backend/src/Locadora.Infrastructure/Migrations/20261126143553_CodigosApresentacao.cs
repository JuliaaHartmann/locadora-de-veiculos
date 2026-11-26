using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations;

public partial class CodigosApresentacao : Migration
{
    // A ordem coincide com os discriminadores persistidos no contador.
    private static readonly string[] Tabelas =
        ["Grupos", "Planos", "Veiculos", "Clientes", "Condutores", "TaxasServico", "PrecosCombustivel", "Alugueis"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var tabela in Tabelas)
            migrationBuilder.Sql($"""
                IF EXISTS (SELECT 1 FROM dbo.[{tabela}] WITH (TABLOCKX, HOLDLOCK)
                    GROUP BY EmpresaId HAVING COUNT_BIG(*) > 999999)
                    THROW 50001, N'O acervo de {tabela} excede a capacidade de códigos por empresa.', 1;
                """);

        migrationBuilder.CreateTable(
            name: "ContadoresCodigo",
            columns: table => new
            {
                EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TipoRecurso = table.Column<byte>(type: "tinyint", nullable: false),
                UltimoNumero = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContadoresCodigo", x => new { x.EmpresaId, x.TipoRecurso });
                table.CheckConstraint("CK_ContadoresCodigo_Numero", "[UltimoNumero] BETWEEN 1 AND 999999");
                table.CheckConstraint("CK_ContadoresCodigo_Tipo", "[TipoRecurso] BETWEEN 1 AND 8");
                table.ForeignKey("FK_ContadoresCodigo_Empresas_EmpresaId", x => x.EmpresaId,
                    "Empresas", "Id", onDelete: ReferentialAction.Restrict);
            });

        for (var i = 0; i < Tabelas.Length; i++)
        {
            var tabela = Tabelas[i];
            migrationBuilder.AddColumn<int>("NumeroExibicao", tabela, type: "int", nullable: true);
            // EXEC adia a resolução da coluna nova em scripts de migração idempotentes.
            migrationBuilder.Sql($"""
                EXEC(N'
                    ;WITH numeracao AS (
                        SELECT NumeroExibicao,
                            ROW_NUMBER() OVER (PARTITION BY EmpresaId ORDER BY CriadoEm, Id) AS Numero
                        FROM dbo.[{tabela}]
                    )
                    UPDATE numeracao SET NumeroExibicao = CONVERT(int, Numero);
                    INSERT dbo.ContadoresCodigo (EmpresaId, TipoRecurso, UltimoNumero)
                        SELECT EmpresaId, {i + 1}, MAX(NumeroExibicao)
                        FROM dbo.[{tabela}] GROUP BY EmpresaId;
                ');
                """);
            migrationBuilder.AlterColumn<int>("NumeroExibicao", tabela, type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.CreateIndex($"IX_{tabela}_EmpresaId_NumeroExibicao", tabela,
                new[] { "EmpresaId", "NumeroExibicao" }, unique: true);
            migrationBuilder.AddCheckConstraint($"CK_{tabela}_NumeroExibicao", tabela, "[NumeroExibicao] BETWEEN 1 AND 999999");
        }
    }

    // Somente em ambientes descartáveis ou antes de expor códigos. Em produção, seguir DT-23.
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ContadoresCodigo");
        foreach (var tabela in Tabelas)
        {
            migrationBuilder.DropIndex($"IX_{tabela}_EmpresaId_NumeroExibicao", tabela);
            migrationBuilder.DropCheckConstraint($"CK_{tabela}_NumeroExibicao", tabela);
            migrationBuilder.DropColumn("NumeroExibicao", tabela);
        }
    }
}
