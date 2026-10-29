using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>vnd_redaction_search_text — извлечённый из Word-файлов текст редакции для поиска
    /// реестра ВНД по полю «Текст редакции». Заполняется VndLegacyLinkIndexer (версия отпечатка
    /// поднята до v3 — все редакции переиндексируются фоновым воркером автоматически).
    ///
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261029090000_AddVndRedactionSearchText")]
    public partial class AddVndRedactionSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_redaction_search_text",
                columns: table => new
                {
                    redaction_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    indexed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_search_text", x => x.redaction_id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_search_text_vnd_redaction_redaction_id",
                        column: x => x.redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_redaction_search_text");
        }
    }
}
