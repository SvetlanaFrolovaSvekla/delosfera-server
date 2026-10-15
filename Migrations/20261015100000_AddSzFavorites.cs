using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>«Избранное» в реестре СЗ: личные отметки пользователя (звёздочка в карточке и в
    /// строке реестра, вкладка «Избранное»). По образцу vnd_favorite. Миграция написана вручную
    /// (без Designer-файла, как и остальные последние миграции проекта), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261015100000_AddSzFavorites")]
    public partial class AddSzFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sz_favorite",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_favorite", x => new { x.user_id, x.sz_document_id });
                    table.ForeignKey(
                        name: "fk_sz_favorite_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_favorite_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_favorite_sz_document_id",
                table: "sz_favorite",
                column: "sz_document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "sz_favorite");
        }
    }
}
