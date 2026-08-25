using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class OrgUnitKindAndUserManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "manager_id",
                table: "user",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "dictionary_organization_unit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 12,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 15,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 18,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 19,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 20,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 21,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 22,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 23,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 24,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 25,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 27,
                column: "kind",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 28,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 29,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 30,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 32,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 34,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                column: "kind",
                value: 1);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 37,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 39,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "manager_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "manager_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_user_manager_id",
                table: "user",
                column: "manager_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_user_manager_id",
                table: "user",
                column: "manager_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_user_manager_id",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_user_manager_id",
                table: "user");

            migrationBuilder.DropColumn(
                name: "manager_id",
                table: "user");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "dictionary_organization_unit");
        }
    }
}
