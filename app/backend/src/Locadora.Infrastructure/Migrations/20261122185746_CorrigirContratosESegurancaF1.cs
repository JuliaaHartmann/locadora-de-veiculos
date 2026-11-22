using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirContratosESegurancaF1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Sessoes_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Sessoes_EmpresaId",
                table: "Sessoes");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Nome",
                table: "Empresas");

            migrationBuilder.AddColumn<string>(
                name: "VersaoAcesso",
                table: "Sessoes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenciaId",
                table: "RecursosTenantTeste",
                type: "uniqueidentifier",
                nullable: true);

            // Preserva o vínculo dos hashes existentes; tokens do contrato anterior não permanecem autorizados.
            migrationBuilder.Sql("""
                UPDATE r SET EmpresaId = s.EmpresaId
                FROM RefreshTokens r INNER JOIN Sessoes s ON s.Id = r.SessaoId;
                UPDATE Sessoes SET RevogadaEm = COALESCE(RevogadaEm, SYSDATETIMEOFFSET());
                UPDATE RefreshTokens SET RevogadoEm = COALESCE(RevogadoEm, SYSDATETIMEOFFSET());
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Usuarios_EmpresaId_Id",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Sessoes_EmpresaId_Id",
                table: "Sessoes",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RecursosTenantTeste_EmpresaId_Id",
                table: "RecursosTenantTeste",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_EmpresaId_UsuarioId",
                table: "Sessoes",
                columns: new[] { "EmpresaId", "UsuarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_EmpresaId_SessaoId",
                table: "RefreshTokens",
                columns: new[] { "EmpresaId", "SessaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecursosTenantTeste_EmpresaId_ReferenciaId",
                table: "RecursosTenantTeste",
                columns: new[] { "EmpresaId", "ReferenciaId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RecursosTenantTeste_Empresas_EmpresaId",
                table: "RecursosTenantTeste",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecursosTenantTeste_RecursosTenantTeste_EmpresaId_ReferenciaId",
                table: "RecursosTenantTeste",
                columns: new[] { "EmpresaId", "ReferenciaId" },
                principalTable: "RecursosTenantTeste",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Sessoes_EmpresaId_SessaoId",
                table: "RefreshTokens",
                columns: new[] { "EmpresaId", "SessaoId" },
                principalTable: "Sessoes",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessoes_Usuarios_EmpresaId_UsuarioId",
                table: "Sessoes",
                columns: new[] { "EmpresaId", "UsuarioId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "EmpresaId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecursosTenantTeste_Empresas_EmpresaId",
                table: "RecursosTenantTeste");

            migrationBuilder.DropForeignKey(
                name: "FK_RecursosTenantTeste_RecursosTenantTeste_EmpresaId_ReferenciaId",
                table: "RecursosTenantTeste");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Sessoes_EmpresaId_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessoes_Usuarios_EmpresaId_UsuarioId",
                table: "Sessoes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Usuarios_EmpresaId_Id",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Sessoes_EmpresaId_Id",
                table: "Sessoes");

            migrationBuilder.DropIndex(
                name: "IX_Sessoes_EmpresaId_UsuarioId",
                table: "Sessoes");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_EmpresaId_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RecursosTenantTeste_EmpresaId_Id",
                table: "RecursosTenantTeste");

            migrationBuilder.DropIndex(
                name: "IX_RecursosTenantTeste_EmpresaId_ReferenciaId",
                table: "RecursosTenantTeste");

            migrationBuilder.DropColumn(
                name: "VersaoAcesso",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReferenciaId",
                table: "RecursosTenantTeste");

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "Empresas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_EmpresaId",
                table: "Sessoes",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessaoId",
                table: "RefreshTokens",
                column: "SessaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Sessoes_SessaoId",
                table: "RefreshTokens",
                column: "SessaoId",
                principalTable: "Sessoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
