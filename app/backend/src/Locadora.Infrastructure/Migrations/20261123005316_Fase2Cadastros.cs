using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase2Cadastros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Versao",
                table: "Empresas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "CamposAlterados",
                table: "Auditorias",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelacaoId",
                table: "Auditorias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntidadeId",
                table: "Auditorias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntidadeTipo",
                table: "Auditorias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Rg = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Cnh = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    EnderecoEstado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnderecoCidade = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnderecoBairro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnderecoRua = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnderecoNumero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.UniqueConstraint("AK_Clientes_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Clientes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                    table.UniqueConstraint("AK_Grupos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Grupos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrecosCombustivel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    InicioVigenciaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Ordem = table.Column<long>(type: "bigint", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosCombustivel", x => x.Id);
                    table.UniqueConstraint("AK_PrecosCombustivel_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_PrecosCombustivel_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seguros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seguros", x => x.Id);
                    table.UniqueConstraint("AK_Seguros_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Seguros_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxasServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxasServico", x => x.Id);
                    table.UniqueConstraint("AK_TaxasServico_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_TaxasServico_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Condutores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Cnh = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ValidadeCnh = table.Column<DateOnly>(type: "date", nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Condutores", x => x.Id);
                    table.UniqueConstraint("AK_Condutores_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Condutores_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Condutores_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VinculosPfPj",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientePfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientePjId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosPfPj", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosPfPj_Clientes_EmpresaId_ClientePfId",
                        columns: x => new { x.EmpresaId, x.ClientePfId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculosPfPj_Clientes_EmpresaId_ClientePjId",
                        columns: x => new { x.EmpresaId, x.ClientePjId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Planos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    PrecoKm = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    FranquiaDiariaKm = table.Column<int>(type: "int", nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planos", x => x.Id);
                    table.UniqueConstraint("AK_Planos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Planos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Planos_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Veiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Combustivel = table.Column<int>(type: "int", nullable: false),
                    CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veiculos", x => x.Id);
                    table.UniqueConstraint("AK_Veiculos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.ForeignKey(
                        name: "FK_Veiculos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Veiculos_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_EmpresaId_OcorridoEm",
                table: "Auditorias",
                columns: new[] { "EmpresaId", "OcorridoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Cnpj",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL AND [Tipo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Cpf",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Cpf" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL AND [Tipo] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Nome",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_EmpresaId_ClienteId",
                table: "Condutores",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_EmpresaId_Cpf",
                table: "Condutores",
                columns: new[] { "EmpresaId", "Cpf" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Grupos_EmpresaId_Nome",
                table: "Grupos",
                columns: new[] { "EmpresaId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos",
                columns: new[] { "EmpresaId", "GrupoId", "Modalidade" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosCombustivel_EmpresaId_Tipo_InicioVigenciaUtc_Ordem_Id",
                table: "PrecosCombustivel",
                columns: new[] { "EmpresaId", "Tipo", "InicioVigenciaUtc", "Ordem", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Seguros_EmpresaId_Tipo",
                table: "Seguros",
                columns: new[] { "EmpresaId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxasServico_EmpresaId_Nome",
                table: "TaxasServico",
                columns: new[] { "EmpresaId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_GrupoId",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "GrupoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_Placa",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Placa" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosPfPj_EmpresaId",
                table: "VinculosPfPj",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosPfPj_EmpresaId_ClientePfId_ClientePjId",
                table: "VinculosPfPj",
                columns: new[] { "EmpresaId", "ClientePfId", "ClientePjId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VinculosPfPj_EmpresaId_ClientePjId",
                table: "VinculosPfPj",
                columns: new[] { "EmpresaId", "ClientePjId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Condutores");

            migrationBuilder.DropTable(
                name: "Planos");

            migrationBuilder.DropTable(
                name: "PrecosCombustivel");

            migrationBuilder.DropTable(
                name: "Seguros");

            migrationBuilder.DropTable(
                name: "TaxasServico");

            migrationBuilder.DropTable(
                name: "Veiculos");

            migrationBuilder.DropTable(
                name: "VinculosPfPj");

            migrationBuilder.DropTable(
                name: "Grupos");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Auditorias_EmpresaId_OcorridoEm",
                table: "Auditorias");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CamposAlterados",
                table: "Auditorias");

            migrationBuilder.DropColumn(
                name: "CorrelacaoId",
                table: "Auditorias");

            migrationBuilder.DropColumn(
                name: "EntidadeId",
                table: "Auditorias");

            migrationBuilder.DropColumn(
                name: "EntidadeTipo",
                table: "Auditorias");

        }
    }
}
