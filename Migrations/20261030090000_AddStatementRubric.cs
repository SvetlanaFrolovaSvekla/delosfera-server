using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <summary>Справочник «Рубрикатор заявлений» (dictionary_statement_rubric) — иерархический,
    /// по образцу Рубрикатора СЗ. Стартовые значения можно править через справочник.
    /// Имя FK обрезано до 63 символов (лимит PostgreSQL) — так же, как его называет EF.
    /// Designer-файла нет, поэтому в InsertData типы колонок указаны явно, а права ролей обновляются SQL.
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261030090000_AddStatementRubric")]
    public partial class AddStatementRubric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dictionary_statement_rubric",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_statement_rubric", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_statement_rubric_dictionary_statement_rubric_par",
                        column: x => x.parent_id,
                        principalTable: "dictionary_statement_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "dictionary_statement_rubric",
                columns: new[] { "id", "created_at", "parent_id", "title_en", "title_kg", "title_ru", "updated_at" },
                columnTypes: new[] { "integer", "timestamp with time zone", "integer", "text", "text", "text", "timestamp with time zone" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "HR matters", "Кадрдык маселелер", "Кадровые вопросы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Certificates and document copies", "Маалым каттар жана документтердин көчүрмөлөрү", "Справки и копии документов", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Financial matters", "Финансылык маселелер", "Финансовые вопросы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Other", "Башка", "Прочее", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Leave", "Өргүүлөр", "Отпуска", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Hiring, transfer, dismissal", "Ишке алуу, которуу, бошотуу", "Приём, перевод, увольнение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, "Financial assistance", "Материалдык жардам", "Материальная помощь", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            // Сид вставляет id явно — сдвигаем identity, иначе первая же новая рубрика
            // из справочника получит id = 1 и упадёт на первичном ключе.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('dictionary_statement_rubric', 'id'), " +
                "(SELECT MAX(id) FROM dictionary_statement_rubric));");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_statement_rubric_parent_id",
                table: "dictionary_statement_rubric",
                column: "parent_id");

            // Сид ролей-администраторов (id 1 и 4) держит полный список прав. В снапшоте он
            // отставал (до 65), теперь догоняем до текущего набора, включая новое право 73
            // «Управление справочниками заявлений».
            migrationBuilder.Sql(
                "UPDATE role SET permission_codes = ARRAY[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73]::integer[] WHERE id IN (1, 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dictionary_statement_rubric");

            migrationBuilder.Sql(
                "UPDATE role SET permission_codes = ARRAY[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65]::integer[] WHERE id IN (1, 4);");
        }
    }
}