using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenameTypeVndToTranslatable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "dictionary_type_vnd",
                newName: "title_ru");

            migrationBuilder.AddColumn<string>(
                name: "title_en",
                table: "dictionary_type_vnd",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_kg",
                table: "dictionary_type_vnd",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Basic Terms of Credit Product", "Кредиттик продукттун негизги шарттары" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Agreement", "Келишим" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Job Description", "Кызматтык нускама" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Instruction", "Нускама" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Code", "Кодекс" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Concept", "Концепция" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Limits", "Лимиттер" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Matrix", "Матрица" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Methodology", "Методика" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Plan", "План" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Policy", "Саясат" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Regulation", "Жобо" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Procedure", "Тартип" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Rules", "Эрежелер" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Program", "Программа" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Process", "Жараян" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Regulations", "Регламент" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Manual", "Жетекчилик" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "System", "Система" });

            migrationBuilder.UpdateData(
                table: "dictionary_type_vnd",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Charter", "Устав" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title_en",
                table: "dictionary_type_vnd");

            migrationBuilder.DropColumn(
                name: "title_kg",
                table: "dictionary_type_vnd");

            migrationBuilder.RenameColumn(
                name: "title_ru",
                table: "dictionary_type_vnd",
                newName: "name");
        }
    }
}
