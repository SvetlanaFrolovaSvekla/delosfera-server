using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>"Избранное" в реестре ВНД: личные отметки пользователя (звёздочка на странице
    /// документа, вкладка "Избранное" в реестре). Миграция написана вручную (без Designer-файла),
    /// снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924190000_VndFavorites")]
    public partial class VndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_favorite",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_favorite", x => new { x.user_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_favorite_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_favorite_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_favorite_vnd_id",
                table: "vnd_favorite",
                column: "vnd_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_favorite");
        }
    }
}
