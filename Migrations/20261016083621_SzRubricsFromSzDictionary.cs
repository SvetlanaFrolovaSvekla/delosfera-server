using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzRubricsFromSzDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sz_document_sz_rubric",
                columns: table => new
                {
                    rubrics_id = table.Column<int>(type: "integer", nullable: false),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_document_sz_rubric", x => new { x.rubrics_id, x.sz_document_id });
                    table.ForeignKey(
                        name: "fk_sz_document_sz_rubric_dictionary_sz_rubric_rubrics_id",
                        column: x => x.rubrics_id,
                        principalTable: "dictionary_sz_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_document_sz_rubric_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_sz_rubric_sz_document_id",
                table: "sz_document_sz_rubric",
                column: "sz_document_id");

            // Записки раньше привязывались к рубрикам ВНД. Переносим то, что можно
            // сопоставить: рубрика СЗ с тем же названием (без учёта регистра и пробелов).
            migrationBuilder.Sql("""
                INSERT INTO sz_document_sz_rubric (rubrics_id, sz_document_id)
                SELECT DISTINCT sr.id, old.sz_document_id
                FROM sz_document_rubric old
                JOIN dictionary_rubric r ON r.id = old.rubrics_id
                JOIN dictionary_sz_rubric sr ON lower(btrim(sr.title_ru)) = lower(btrim(r.title_ru))
                ON CONFLICT DO NOTHING;
                """);

            // Старые связи не удаляем: несопоставленные рубрики иначе пропали бы без
            // следа. Таблица остаётся под другим именем — модель её больше не знает,
            // её можно разобрать вручную и удалить отдельной миграцией.
            migrationBuilder.RenameTable(
                name: "sz_document_rubric",
                newName: "sz_document_rubric_vnd_legacy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sz_document_sz_rubric");

            migrationBuilder.RenameTable(
                name: "sz_document_rubric_vnd_legacy",
                newName: "sz_document_rubric");
        }
    }
}
