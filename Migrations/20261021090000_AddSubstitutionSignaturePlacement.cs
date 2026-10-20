using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Место подписи ЭЦП под приказом/договором заявки на замещение
    /// (substitution_signatures.placement_anchor / placement_indent_mm): подписант сам выбирает в окне
    /// подписания, после какого абзаца документа и с каким отступом стоит его блок подписи. У прежних
    /// подписей места нет (NULL) — блок ставится в конце текста. Журнал подписей append-only (триггер
    /// запрещает UPDATE/DELETE), добавление столбцов он не затрагивает. Миграция написана вручную (без
    /// Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261021090000_AddSubstitutionSignaturePlacement")]
    public partial class AddSubstitutionSignaturePlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "placement_anchor",
                table: "substitution_signatures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "placement_indent_mm",
                table: "substitution_signatures",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "placement_anchor",
                table: "substitution_signatures");

            migrationBuilder.DropColumn(
                name: "placement_indent_mm",
                table: "substitution_signatures");
        }
    }
}
