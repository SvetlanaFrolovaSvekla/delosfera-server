using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenumberPermissionCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 2,
                column: "permission_codes",
                value: new[] { 11, 15 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 3,
                column: "permission_codes",
                value: new[] { 1, 4, 5, 6, 10, 21, 11, 12, 16, 17, 18 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 2,
                column: "permission_codes",
                value: new[] { 12, 16 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 3,
                column: "permission_codes",
                value: new[] { 1, 4, 5, 6, 10, 11, 12, 13, 17, 18, 19 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });
        }
    }
}
