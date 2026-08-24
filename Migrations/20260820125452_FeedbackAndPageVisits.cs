using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackAndPageVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedback_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    route_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: true),
                    page_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    user_agent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    viewport_width = table.Column<int>(type: "integer", nullable: true),
                    viewport_height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    handler_comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    handled_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feedback_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_feedback_item_users_handled_by_user_id",
                        column: x => x.handled_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_feedback_item_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "page_visit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    route_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    visited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    session_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_visit", x => x.id);
                    table.ForeignKey(
                        name: "fk_page_visit_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feedback_item_handled_by_user_id",
                table: "feedback_item",
                column: "handled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_item_route_path",
                table: "feedback_item",
                column: "route_path");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_item_status_created_at",
                table: "feedback_item",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_feedback_item_user_id",
                table: "feedback_item",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_page_visit_route_path_visited_at",
                table: "page_visit",
                columns: new[] { "route_path", "visited_at" });

            migrationBuilder.CreateIndex(
                name: "ix_page_visit_user_id_visited_at",
                table: "page_visit",
                columns: new[] { "user_id", "visited_at" });

            migrationBuilder.CreateIndex(
                name: "ix_page_visit_visited_at",
                table: "page_visit",
                column: "visited_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback_item");

            migrationBuilder.DropTable(
                name: "page_visit");
        }
    }
}
