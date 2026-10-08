using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Секретность ВНД (право ManageVndSecrecy): флаг is_secret и два списка допуска —
    /// группы пользователей (vnd_secret_group) и отдельные пользователи (vnd_secret_user).
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261008130000_AddVndSecrecy")]
    public partial class AddVndSecrecy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_secret",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "vnd_secret_group",
                columns: table => new
                {
                    user_group_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_secret_group", x => new { x.user_group_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_secret_group_dictionary_user_group_user_group_id",
                        column: x => x.user_group_id,
                        principalTable: "dictionary_user_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_secret_group_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_secret_user",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_secret_user", x => new { x.user_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_secret_user_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_secret_user_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_secret_group_vnd_id",
                table: "vnd_secret_group",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_secret_user_vnd_id",
                table: "vnd_secret_user",
                column: "vnd_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_secret_group");
            migrationBuilder.DropTable(name: "vnd_secret_user");
            migrationBuilder.DropColumn(name: "is_secret", table: "vnd_document");
        }
    }
}
