using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndApprovalStageQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_approval_stage_quote",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_approval_stage_id = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    document_target = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_stage_quote", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_quote_vnd_approval_stage_id",
                        column: x => x.vnd_approval_stage_id,
                        principalTable: "vnd_approval_stage",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_quote_vnd_approval_stage_id_phase",
                table: "vnd_approval_stage_quote",
                columns: new[] { "vnd_approval_stage_id", "phase" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_approval_stage_quote");
        }
    }
}
