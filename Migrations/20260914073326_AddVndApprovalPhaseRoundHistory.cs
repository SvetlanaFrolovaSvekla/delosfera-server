using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndApprovalPhaseRoundHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "vnd_approval_stage",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "vnd_approval_phase_round",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_process_id = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    initiator_comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_phase_round", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_phase_round_vnd_approval_processes_approval_pr",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_approval_phase_round_stage_decision",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_approval_phase_round_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_approval_stage_id = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_phase_round_stage_decision", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_approval_phase_round_stage_decision_vnd_approval_phase_",
                        column: x => x.vnd_approval_phase_round_id,
                        principalTable: "vnd_approval_phase_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_approval_phase_round_stage_decision_vnd_approval_stages",
                        column: x => x.vnd_approval_stage_id,
                        principalTable: "vnd_approval_stage",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_phase_round_approval_process_id_phase_round_nu",
                table: "vnd_approval_phase_round",
                columns: new[] { "approval_process_id", "phase", "round_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_phase_round_stage_decision_vnd_approval_phase_",
                table: "vnd_approval_phase_round_stage_decision",
                columns: new[] { "vnd_approval_phase_round_id", "vnd_approval_stage_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_phase_round_stage_decision_vnd_approval_stage_",
                table: "vnd_approval_phase_round_stage_decision",
                column: "vnd_approval_stage_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_approval_phase_round_stage_decision");

            migrationBuilder.DropTable(
                name: "vnd_approval_phase_round");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "vnd_approval_stage");
        }
    }
}
