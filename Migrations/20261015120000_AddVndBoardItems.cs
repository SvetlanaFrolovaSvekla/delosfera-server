using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>«Доска ВНД»: ручные поправки пользователя к составу доски — документы, добавленные
    /// вручную (mode = 1), и связанные с СП документы, убранные с доски (mode = 2). По образцу
    /// vnd_favorite. Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261015120000_AddVndBoardItems")]
    public partial class AddVndBoardItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_board_item",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_board_item", x => new { x.user_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_board_item_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_board_item_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_board_item_vnd_id",
                table: "vnd_board_item",
                column: "vnd_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_board_item");
        }
    }
}
