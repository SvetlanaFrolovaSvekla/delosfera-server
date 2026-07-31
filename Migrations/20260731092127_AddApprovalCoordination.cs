using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalCoordination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                column: "title_ru",
                value: "Управление методологии");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                column: "title_ru",
                value: "Управление методологии и продуктов");
        }
    }
}
