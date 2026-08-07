using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class UodVndDocumentConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "created_by_user_id",
                value: 14);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "created_by_user_id",
                value: 14);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "created_by_user_id",
                value: 7);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "created_by_user_id",
                value: 15);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "created_by_user_id",
                value: 12);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "created_by_user_id",
                value: 17);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "created_by_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "created_by_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "created_by_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "created_by_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "created_by_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "created_by_user_id",
                value: null);
        }
    }
}
