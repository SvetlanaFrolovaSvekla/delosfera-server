using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstitutionApprovals : Migration
    {
        // Миграция вручную сокращена до одной таблицы substitution_approvals: авто-генерация
        // подхватила дрейф снапшота (vnd_*, dictionary_sz_rubric, notification-колонки),
        // которые уже есть в БД от других миграций — их создание здесь ломало бы применение.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "substitution_approvals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    role_label = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitution_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitution_approvals_substitution_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "substitution_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_substitution_approvals_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_substitution_approvals_request_id",
                table: "substitution_approvals",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_approvals_user_id",
                table: "substitution_approvals",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "substitution_approvals");
        }
    }
}
