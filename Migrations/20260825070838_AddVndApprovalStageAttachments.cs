using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndApprovalStageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_approval_stage_attachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_approval_stage_id = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    file_attachment_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_stage_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_attachment_file_attachments_file_attachm",
                        column: x => x.file_attachment_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_attachment_vnd_approval_stages_vnd_appro",
                        column: x => x.vnd_approval_stage_id,
                        principalTable: "vnd_approval_stage",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_attachment_file_attachment_id",
                table: "vnd_approval_stage_attachment",
                column: "file_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_attachment_vnd_approval_stage_id_phase_f",
                table: "vnd_approval_stage_attachment",
                columns: new[] { "vnd_approval_stage_id", "phase", "file_attachment_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_approval_stage_attachment");
        }
    }
}
