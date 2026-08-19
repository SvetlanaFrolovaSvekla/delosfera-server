using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "sz_document",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(body, '') || ' ' || coalesce(execution_resolution, ''))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "procurement_request",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(subject, '') || ' ' || coalesce(justification, '') || ' ' || coalesce(plan_item, ''))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "meeting_agenda_item",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(topic, '') || ' ' || coalesce(decision, '') || ' ' || coalesce(protocol_number, ''))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "document",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(title, '') || ' ' || coalesce(reg_number, ''))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "saved_search",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    criteria = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_search", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_search_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_search_vector",
                table: "sz_document",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_search_vector",
                table: "procurement_request",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_search_vector",
                table: "meeting_agenda_item",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_document_search_vector",
                table: "document",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_saved_search_user_id_name",
                table: "saved_search",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_search");

            migrationBuilder.DropIndex(
                name: "ix_sz_document_search_vector",
                table: "sz_document");

            migrationBuilder.DropIndex(
                name: "ix_procurement_request_search_vector",
                table: "procurement_request");

            migrationBuilder.DropIndex(
                name: "ix_meeting_agenda_item_search_vector",
                table: "meeting_agenda_item");

            migrationBuilder.DropIndex(
                name: "ix_document_search_vector",
                table: "document");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "procurement_request");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "meeting_agenda_item");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "document");
        }
    }
}
