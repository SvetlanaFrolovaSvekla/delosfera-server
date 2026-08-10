using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLdapLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ldap_login",
                table: "user",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "ldap_login",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "ldap_login",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ldap_login",
                table: "user");
        }
    }
}
