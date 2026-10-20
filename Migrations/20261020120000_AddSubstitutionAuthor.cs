using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Автор заявки на замещение (substitution_requests.author_user_id): кто завёл
    /// заявку в системе, ставится при создании и больше не меняется. Для существующих заявок
    /// автор восстанавливается из журнала аудита (действие «Created»), а если записи нет —
    /// берётся инициатор. Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261020120000_AddSubstitutionAuthor")]
    public partial class AddSubstitutionAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "author_user_id",
                table: "substitution_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE substitution_requests r
                SET author_user_id = COALESCE(
                    (SELECT a.user_id
                       FROM audit_entry a
                      WHERE a.entity_type = 'SubstitutionRequest'
                        AND a.entity_id = r.id
                        AND a.action = 'Created'
                        AND a.user_id IS NOT NULL
                        AND EXISTS (SELECT 1 FROM "user" u WHERE u.id = a.user_id)
                      ORDER BY a.at
                      LIMIT 1),
                    r.initiator_user_id)
                WHERE r.author_user_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_author_user_id",
                table: "substitution_requests",
                column: "author_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_substitution_requests_users_author_user_id",
                table: "substitution_requests",
                column: "author_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_substitution_requests_users_author_user_id",
                table: "substitution_requests");

            migrationBuilder.DropIndex(
                name: "ix_substitution_requests_author_user_id",
                table: "substitution_requests");

            migrationBuilder.DropColumn(
                name: "author_user_id",
                table: "substitution_requests");
        }
    }
}
