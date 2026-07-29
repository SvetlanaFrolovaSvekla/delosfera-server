using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenameSecurityLevelToTranslatable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "dictionary_security_level",
                newName: "title_ru");

            migrationBuilder.AddColumn<string>(
                name: "title_en",
                table: "dictionary_security_level",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_kg",
                table: "dictionary_security_level",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "dictionary_security_level",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Public Access", "Ачык жеткиликтүүлүк" });

            migrationBuilder.UpdateData(
                table: "dictionary_security_level",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Confidential", "Конфиденциалдуу" });

            migrationBuilder.UpdateData(
                table: "dictionary_security_level",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Secret", "Жашыруун" });

            migrationBuilder.UpdateData(
                table: "dictionary_security_level",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Top Secret", "Өтө жашыруун" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title_en",
                table: "dictionary_security_level");

            migrationBuilder.DropColumn(
                name: "title_kg",
                table: "dictionary_security_level");

            migrationBuilder.RenameColumn(
                name: "title_ru",
                table: "dictionary_security_level",
                newName: "name");
        }
    }
}
