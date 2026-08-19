using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class DocumentAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acknowledgement_sheets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    instruction = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    require_signature = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acknowledgement_sheets", x => x.id);
                    table.ForeignKey(
                        name: "fk_acknowledgement_sheets_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acknowledgement_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sheet_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    signature_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acknowledgement_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_acknowledgement_entries_acknowledgement_sheets_sheet_id",
                        column: x => x.sheet_id,
                        principalTable: "acknowledgement_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_acknowledgement_entries_organization_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_acknowledgement_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acknowledgement_entries_org_unit_id",
                table: "acknowledgement_entries",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_acknowledgement_entries_sheet_id",
                table: "acknowledgement_entries",
                column: "sheet_id");

            migrationBuilder.CreateIndex(
                name: "ix_acknowledgement_entries_user_id",
                table: "acknowledgement_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_acknowledgement_sheets_document_id",
                table: "acknowledgement_sheets",
                column: "document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acknowledgement_entries");

            migrationBuilder.DropTable(
                name: "acknowledgement_sheets");
        }
    }
}
