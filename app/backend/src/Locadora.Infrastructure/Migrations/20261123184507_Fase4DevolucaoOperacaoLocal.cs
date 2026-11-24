using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase4DevolucaoOperacaoLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinalId",
                table: "IdempotenciaAlugueis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ContratosSnapshot_EmpresaId_AluguelId_Id",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "AluguelId", "Id" });

            migrationBuilder.CreateTable(
                name: "FechamentosAlugueis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AluguelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoRegra = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RetornoRealUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HodometroInicial = table.Column<int>(type: "int", nullable: false),
                    HodometroFinal = table.Column<int>(type: "int", nullable: false),
                    CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    LitrosRestantes = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    LitrosARepor = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Diarias = table.Column<int>(type: "int", nullable: false),
                    KmRodados = table.Column<int>(type: "int", nullable: false),
                    KmCobrados = table.Column<long>(type: "bigint", nullable: false),
                    FranquiaTotalKm = table.Column<long>(type: "bigint", nullable: true),
                    DiariasBruto = table.Column<decimal>(type: "decimal(26,9)", precision: 26, scale: 9, nullable: false),
                    QuilometragemBruto = table.Column<decimal>(type: "decimal(26,9)", precision: 26, scale: 9, nullable: false),
                    ValorPlano = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    PrecoCombustivelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Combustivel = table.Column<int>(type: "int", nullable: false),
                    InicioVigenciaCombustivelUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OrdemCombustivel = table.Column<long>(type: "bigint", nullable: false),
                    PrecoPorLitro = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    CombustivelBruto = table.Column<decimal>(type: "decimal(26,9)", precision: 26, scale: 9, nullable: false),
                    ValorCombustivel = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    MultaBruto = table.Column<decimal>(type: "decimal(26,9)", precision: 26, scale: 9, nullable: true),
                    ValorMulta = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Caucao = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosAlugueis", x => x.Id);
                    table.UniqueConstraint("AK_FechamentosAlugueis_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.UniqueConstraint("AK_FechamentosAlugueis_EmpresaId_Id_RevisaoId", x => new { x.EmpresaId, x.Id, x.RevisaoId });
                    table.CheckConstraint("CK_FechamentosAlugueis_Medicoes", "[Diarias] > 0 AND [HodometroInicial] >= 0 AND [HodometroFinal] >= [HodometroInicial] AND [KmRodados] = [HodometroFinal] - [HodometroInicial] AND [KmCobrados] BETWEEN 0 AND [KmRodados] AND [FranquiaTotalKm] >= 0 AND [CapacidadeLitros] > 0 AND [LitrosRestantes] BETWEEN 0 AND [CapacidadeLitros] AND [LitrosARepor] = [CapacidadeLitros] - [LitrosRestantes] AND [InicioVigenciaCombustivelUtc] <= [RetornoRealUtc]");
                    table.CheckConstraint("CK_FechamentosAlugueis_Valores", "[DiariasBruto] >= 0 AND [QuilometragemBruto] >= 0 AND [ValorPlano] >= 0 AND [PrecoPorLitro] >= 0 AND [CombustivelBruto] >= 0 AND [ValorCombustivel] >= 0 AND (([MultaBruto] IS NULL AND [ValorMulta] IS NULL) OR ([MultaBruto] IS NOT NULL AND [ValorMulta] IS NOT NULL AND [MultaBruto] >= 0 AND [ValorMulta] >= 0)) AND [Total] >= 0 AND [Caucao] = 1000 AND [ExcluidoEmUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_FechamentosAlugueis_Alugueis_EmpresaId_AluguelId",
                        columns: x => new { x.EmpresaId, x.AluguelId },
                        principalTable: "Alugueis",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosAlugueis_ContratosSnapshot_EmpresaId_AluguelId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.AluguelId, x.RevisaoId },
                        principalTable: "ContratosSnapshot",
                        principalColumns: new[] { "EmpresaId", "AluguelId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosAlugueis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FechamentosAlugueis_PrecosCombustivel_EmpresaId_PrecoCombustivelId",
                        columns: x => new { x.EmpresaId, x.PrecoCombustivelId },
                        principalTable: "PrecosCombustivel",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensFechamentoAluguel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SeguroId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersaoTaxaAdicional = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Bruto = table.Column<decimal>(type: "decimal(26,9)", precision: 26, scale: 9, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensFechamentoAluguel", x => x.Id);
                    table.UniqueConstraint("AK_ItensFechamentoAluguel_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ItensFechamentoAluguel_Origem", "(([Categoria] = 0 AND [TaxaId] IS NOT NULL AND [SeguroId] IS NULL AND [Modalidade] IN (0, 1)) OR ([Categoria] IN (1, 2) AND [TaxaId] IS NULL AND [SeguroId] IS NOT NULL AND [VersaoTaxaAdicional] IS NULL AND [Modalidade] = 1)) AND [Quantidade] > 0 AND ([Modalidade] <> 0 OR [Quantidade] = 1) AND [Preco] >= 0 AND [Bruto] >= 0 AND [Valor] >= 0 AND [ExcluidoEmUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_ItensFechamentoAluguel_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensFechamentoAluguel_FechamentosAlugueis_EmpresaId_FinalId",
                        columns: x => new { x.EmpresaId, x.FinalId },
                        principalTable: "FechamentosAlugueis",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensFechamentoAluguel_Seguros_EmpresaId_SeguroId",
                        columns: x => new { x.EmpresaId, x.SeguroId },
                        principalTable: "Seguros",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensFechamentoAluguel_TaxasServico_EmpresaId_TaxaId",
                        columns: x => new { x.EmpresaId, x.TaxaId },
                        principalTable: "TaxasServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaAlugueis_EmpresaId_FinalId_RevisaoId",
                table: "IdempotenciaAlugueis",
                columns: new[] { "EmpresaId", "FinalId", "RevisaoId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_IdempotenciaAlugueis_Resultado",
                table: "IdempotenciaAlugueis",
                sql: "([Operacao] = 'criar' AND [FinalId] IS NULL) OR ([Operacao] = 'devolver' AND [FinalId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Alugueis_Estado",
                table: "Alugueis",
                sql: "[Estado] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosAlugueis_EmpresaId_AluguelId",
                table: "FechamentosAlugueis",
                columns: new[] { "EmpresaId", "AluguelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosAlugueis_EmpresaId_AluguelId_RevisaoId",
                table: "FechamentosAlugueis",
                columns: new[] { "EmpresaId", "AluguelId", "RevisaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosAlugueis_EmpresaId_PrecoCombustivelId",
                table: "FechamentosAlugueis",
                columns: new[] { "EmpresaId", "PrecoCombustivelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensFechamentoAluguel_EmpresaId_FinalId_Categoria",
                table: "ItensFechamentoAluguel",
                columns: new[] { "EmpresaId", "FinalId", "Categoria" },
                unique: true,
                filter: "[SeguroId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensFechamentoAluguel_EmpresaId_FinalId_TaxaId",
                table: "ItensFechamentoAluguel",
                columns: new[] { "EmpresaId", "FinalId", "TaxaId" },
                unique: true,
                filter: "[TaxaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensFechamentoAluguel_EmpresaId_SeguroId",
                table: "ItensFechamentoAluguel",
                columns: new[] { "EmpresaId", "SeguroId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensFechamentoAluguel_EmpresaId_TaxaId",
                table: "ItensFechamentoAluguel",
                columns: new[] { "EmpresaId", "TaxaId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotenciaAlugueis_FechamentosAlugueis_EmpresaId_FinalId_RevisaoId",
                table: "IdempotenciaAlugueis",
                columns: new[] { "EmpresaId", "FinalId", "RevisaoId" },
                principalTable: "FechamentosAlugueis",
                principalColumns: new[] { "EmpresaId", "Id", "RevisaoId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotenciaAlugueis_FechamentosAlugueis_EmpresaId_FinalId_RevisaoId",
                table: "IdempotenciaAlugueis");

            migrationBuilder.DropTable(
                name: "ItensFechamentoAluguel");

            migrationBuilder.DropTable(
                name: "FechamentosAlugueis");

            migrationBuilder.DropIndex(
                name: "IX_IdempotenciaAlugueis_EmpresaId_FinalId_RevisaoId",
                table: "IdempotenciaAlugueis");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IdempotenciaAlugueis_Resultado",
                table: "IdempotenciaAlugueis");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ContratosSnapshot_EmpresaId_AluguelId_Id",
                table: "ContratosSnapshot");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Alugueis_Estado",
                table: "Alugueis");

            migrationBuilder.DropColumn(
                name: "FinalId",
                table: "IdempotenciaAlugueis");
        }
    }
}
