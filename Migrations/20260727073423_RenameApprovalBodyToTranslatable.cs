using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenameApprovalBodyToTranslatable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "dictionary_approval_body",
                newName: "title_ru");

            migrationBuilder.AddColumn<string>(
                name: "title_en",
                table: "dictionary_approval_body",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_kg",
                table: "dictionary_approval_body",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Asset and Liability Management Committee", "Активдерди жана пассивдерди башкаруу комитети" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "General Meeting of Shareholders", "Акционерлердин жалпы жыйыны" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Management Board", "Башкарма" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Deputy Chairman of the Management Board", "Башкарманын төрагасынын орун басары" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Chairman of the Management Board", "Башкарманын төрагасы" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Member of the Management Board", "Башкарманын мүчөсү" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Board of Directors", "Директорлор кеңеши" });

            migrationBuilder.UpdateData(
                table: "dictionary_approval_body",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { "Tariff Committee", "Тарифтик комитет" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "title_en",
                table: "dictionary_approval_body");

            migrationBuilder.DropColumn(
                name: "title_kg",
                table: "dictionary_approval_body");

            migrationBuilder.RenameColumn(
                name: "title_ru",
                table: "dictionary_approval_body",
                newName: "name");
        }
    }
}
