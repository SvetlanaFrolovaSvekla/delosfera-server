using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>ИНН организации в сохранённой ЭЦП-идентификации (подпись от имени организации).
    /// Написана вручную, без Designer-файла; снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260929130000_AddUserEcpCredentialOrganizationInn")]
    public partial class AddUserEcpCredentialOrganizationInn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "organization_inn",
                table: "user_ecp_credential",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "organization_inn", table: "user_ecp_credential");
        }
    }
}
