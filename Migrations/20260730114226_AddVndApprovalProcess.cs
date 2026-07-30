using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndApprovalProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_approval_process",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    redaction_id = table.Column<int>(type: "integer", nullable: false),
                    initiator_user_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    primary_deadline_hours = table.Column<int>(type: "integer", nullable: false),
                    repeat_deadline_hours = table.Column<int>(type: "integer", nullable: false),
                    final_hold_deadline_hours = table.Column<int>(type: "integer", nullable: false),
                    primary_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    repeat_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    final_hold_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_process", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_process_vnd_documents_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_approval_process_vnd_redactions_redaction_id",
                        column: x => x.redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vnd_approval_stage",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_process_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: false),
                    approver_user_id = table.Column<int>(type: "integer", nullable: false),
                    primary_decision = table.Column<int>(type: "integer", nullable: false),
                    primary_comment = table.Column<string>(type: "text", nullable: true),
                    primary_decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    participates_in_repeat = table.Column<bool>(type: "boolean", nullable: false),
                    repeat_decision = table.Column<int>(type: "integer", nullable: true),
                    repeat_comment = table.Column<string>(type: "text", nullable: true),
                    repeat_decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_stage", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_dictionary_organization_unit_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_user_approver_user_id",
                        column: x => x.approver_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_approval_stage_vnd_approval_process_approval_process_id",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_process_redaction_id",
                table: "vnd_approval_process",
                column: "redaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_process_vnd_id",
                table: "vnd_approval_process",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_approval_process_id_order",
                table: "vnd_approval_stage",
                columns: new[] { "approval_process_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_approver_user_id",
                table: "vnd_approval_stage",
                column: "approver_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_org_unit_id",
                table: "vnd_approval_stage",
                column: "org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_approval_stage");

            migrationBuilder.DropTable(
                name: "vnd_approval_process");
        }
    }
}
