using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementRouteHeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { 14, 8 });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { 5, 7 });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { 5, 4 });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { 5, 5 });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { 7, 11 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });
        }
    }
}
