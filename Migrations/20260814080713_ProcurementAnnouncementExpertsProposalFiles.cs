using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementAnnouncementExpertsProposalFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "announcement_from",
                table: "procurement_request",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "announcement_to",
                table: "procurement_request",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_link",
                table: "procurement_proposal",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conclusion",
                table: "procurement_commission_member",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "conclusion_at",
                table: "procurement_commission_member",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "conclusion_attachment_id",
                table: "procurement_commission_member",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "procurement_proposal_file",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    document_attachment_id = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_proposal_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_proposal_file_procurement_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "procurement_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_proposal_file_proposal_id_document_attachment_id",
                table: "procurement_proposal_file",
                columns: new[] { "proposal_id", "document_attachment_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_proposal_file");

            migrationBuilder.DropColumn(
                name: "announcement_from",
                table: "procurement_request");

            migrationBuilder.DropColumn(
                name: "announcement_to",
                table: "procurement_request");

            migrationBuilder.DropColumn(
                name: "external_link",
                table: "procurement_proposal");

            migrationBuilder.DropColumn(
                name: "conclusion",
                table: "procurement_commission_member");

            migrationBuilder.DropColumn(
                name: "conclusion_at",
                table: "procurement_commission_member");

            migrationBuilder.DropColumn(
                name: "conclusion_attachment_id",
                table: "procurement_commission_member");
        }
    }
}
