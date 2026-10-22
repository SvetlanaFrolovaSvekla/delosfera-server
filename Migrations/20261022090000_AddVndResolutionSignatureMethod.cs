using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Способ подписи резолюции ВНД (vnd_resolution_signature.method): 0 — ЭЦП ГП «Инфоком»
    /// (Түндүк), 1 — локальная подпись системы «Делосфера» для сотрудников без ЭЦП КР (право
    /// SignWithoutNationalEcp = 66, см. ResolutionSignatureMethod).
    ///
    /// Все существующие записи — ЭЦП (или тестовая имитация ЭЦП), поэтому столбец добавляется с
    /// DEFAULT 0. В PostgreSQL 11+ ADD COLUMN с константным DEFAULT не переписывает строки, поэтому
    /// append-only триггер журнала (trg_vnd_resolution_signature_append_only, BEFORE UPDATE OR
    /// DELETE) не срабатывает. Миграция написана вручную (без Designer-файла), снапшот модели
    /// обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261022090000_AddVndResolutionSignatureMethod")]
    public partial class AddVndResolutionSignatureMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "method",
                table: "vnd_resolution_signature",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "method",
                table: "vnd_resolution_signature");
        }
    }
}
