using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3ContratacaoCalculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alugueis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaidaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevisaoAtual = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alugueis", x => x.Id);
                    table.UniqueConstraint("AK_Alugueis_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Alugueis_AbertoNaoOcultado", "[Estado] <> 0 OR [ExcluidoEmUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_Alugueis_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alugueis_Condutores_EmpresaId_CondutorId",
                        columns: x => new { x.EmpresaId, x.CondutorId },
                        principalTable: "Condutores",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alugueis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alugueis_Planos_EmpresaId_PlanoId",
                        columns: x => new { x.EmpresaId, x.PlanoId },
                        principalTable: "Planos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alugueis_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContratosSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AluguelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteTipo = table.Column<int>(type: "int", nullable: false),
                    ClienteNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClienteDocumento = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    CondutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondutorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CondutorCpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    CondutorCnh = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ValidadeCnh = table.Column<DateOnly>(type: "date", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoPlaca = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VeiculoMarca = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VeiculoModelo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Combustivel = table.Column<int>(type: "int", nullable: false),
                    CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModalidadePlano = table.Column<int>(type: "int", nullable: false),
                    PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    PrecoKm = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    FranquiaDiariaKm = table.Column<int>(type: "int", nullable: true),
                    RetornoPrevistoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HodometroInicial = table.Column<int>(type: "int", nullable: false),
                    Diarias = table.Column<int>(type: "int", nullable: false),
                    ValorPlano = table.Column<decimal>(type: "decimal(28,2)", precision: 28, scale: 2, nullable: false),
                    FranquiaTotalKm = table.Column<long>(type: "bigint", nullable: true),
                    ParcelasConhecidas = table.Column<decimal>(type: "decimal(28,2)", precision: 28, scale: 2, nullable: false),
                    Caucao = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContratosSnapshot", x => x.Id);
                    table.UniqueConstraint("AK_ContratosSnapshot_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Alugueis_EmpresaId_AluguelId",
                        columns: x => new { x.EmpresaId, x.AluguelId },
                        principalTable: "Alugueis",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Condutores_EmpresaId_CondutorId",
                        columns: x => new { x.EmpresaId, x.CondutorId },
                        principalTable: "Condutores",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Planos_EmpresaId_PlanoId",
                        columns: x => new { x.EmpresaId, x.PlanoId },
                        principalTable: "Planos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContratosSnapshot_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotenciaAlugueis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operacao = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Chave = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    HashComando = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoOriginal = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotenciaAlugueis", x => x.Id);
                    table.UniqueConstraint("AK_IdempotenciaAlugueis_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_IdempotenciaAlugueis_ContratosSnapshot_EmpresaId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.RevisaoId },
                        principalTable: "ContratosSnapshot",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdempotenciaAlugueis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdempotenciaAlugueis_Usuarios_EmpresaId_UsuarioId",
                        columns: x => new { x.EmpresaId, x.UsuarioId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensContratados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SeguroId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(28,2)", precision: 28, scale: 2, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensContratados", x => x.Id);
                    table.UniqueConstraint("AK_ItensContratados_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ItensContratados_Origem", "([Categoria] = 0 AND [TaxaId] IS NOT NULL AND [SeguroId] IS NULL) OR ([Categoria] IN (1, 2) AND [TaxaId] IS NULL AND [SeguroId] IS NOT NULL AND [Modalidade] = 1)");
                    table.ForeignKey(
                        name: "FK_ItensContratados_ContratosSnapshot_EmpresaId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.RevisaoId },
                        principalTable: "ContratosSnapshot",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensContratados_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensContratados_Seguros_EmpresaId_SeguroId",
                        columns: x => new { x.EmpresaId, x.SeguroId },
                        principalTable: "Seguros",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensContratados_TaxasServico_EmpresaId_TaxaId",
                        columns: x => new { x.EmpresaId, x.TaxaId },
                        principalTable: "TaxasServico",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_ClienteId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_CondutorId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "CondutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_Estado_SaidaUtc_Id",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "Estado", "SaidaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_PlanoId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "PlanoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_VeiculoId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "VeiculoId" },
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_AluguelId_Numero",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "AluguelId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_ClienteId",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_CondutorId",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "CondutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_GrupoId",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "GrupoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_PlanoId",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "PlanoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContratosSnapshot_EmpresaId_VeiculoId",
                table: "ContratosSnapshot",
                columns: new[] { "EmpresaId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaAlugueis_EmpresaId_RevisaoId",
                table: "IdempotenciaAlugueis",
                columns: new[] { "EmpresaId", "RevisaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaAlugueis_EmpresaId_UsuarioId_Operacao_Chave",
                table: "IdempotenciaAlugueis",
                columns: new[] { "EmpresaId", "UsuarioId", "Operacao", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_RevisaoId_Categoria",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "RevisaoId", "Categoria" },
                unique: true,
                filter: "[SeguroId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_RevisaoId_TaxaId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "RevisaoId", "TaxaId" },
                unique: true,
                filter: "[TaxaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_SeguroId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "SeguroId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_TaxaId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "TaxaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotenciaAlugueis");

            migrationBuilder.DropTable(
                name: "ItensContratados");

            migrationBuilder.DropTable(
                name: "ContratosSnapshot");

            migrationBuilder.DropTable(
                name: "Alugueis");
        }
    }
}
