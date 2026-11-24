using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase5FotosVeiculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FotosObjetos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UrlRelativa = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Deposito = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: false),
                    Chave = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Mime = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    RetomarAposUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TentativasCompensacao = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosObjetos", x => x.Id);
                    table.UniqueConstraint("AK_FotosObjetos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_FotosObjetos_Conteudo", "[Tamanho] > 0 AND [Mime] IN ('image/jpeg', 'image/png') AND [TentativasCompensacao] >= 0");
                    table.CheckConstraint("CK_FotosObjetos_Estado", "[Estado] BETWEEN 0 AND 5 AND [ExcluidoEmUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_FotosObjetos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FotosObjetos_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_Deposito_Chave",
                table: "FotosObjetos",
                columns: new[] { "Deposito", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_EmpresaId_Deposito_Estado_RetomarAposUtc",
                table: "FotosObjetos",
                columns: new[] { "EmpresaId", "Deposito", "Estado", "RetomarAposUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_EmpresaId_VeiculoId",
                table: "FotosObjetos",
                columns: new[] { "EmpresaId", "VeiculoId" },
                unique: true,
                filter: "[Estado] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosObjetos");
        }
    }
}
