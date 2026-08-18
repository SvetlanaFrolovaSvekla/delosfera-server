using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class HelpArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "help_article",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    section = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_kg = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    summary_ru = table.Column<string>(type: "text", nullable: true),
                    summary_kg = table.Column<string>(type: "text", nullable: true),
                    body_json = table.Column<string>(type: "text", nullable: false),
                    route_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_help_article", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_help_article_route_path",
                table: "help_article",
                column: "route_path");

            migrationBuilder.CreateIndex(
                name: "ix_help_article_section_sort_order",
                table: "help_article",
                columns: new[] { "section", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "help_article");
        }
    }
}
