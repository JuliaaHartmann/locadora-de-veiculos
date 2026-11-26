using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailOpcionalCondutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Condutores",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.Condutores WITH (TABLOCKX, HOLDLOCK) WHERE Email IS NULL)
                    THROW 51024, N'Não é possível reverter: existem condutores sem e-mail.', 1;

                ALTER TABLE dbo.Condutores ALTER COLUMN Email nvarchar(254) NOT NULL;
                """);
        }
    }
}
