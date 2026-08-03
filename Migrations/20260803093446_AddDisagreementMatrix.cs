using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddDisagreementMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "final_hold_comment",
                table: "vnd_approval_stage",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "final_hold_decided_at",
                table: "vnd_approval_stage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "final_hold_decision",
                table: "vnd_approval_stage",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vnd_disagreement_matrix_row",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    approval_process_id = table.Column<int>(type: "integer", nullable: false),
                    developer_position = table.Column<string>(type: "text", nullable: false),
                    opponent_position = table.Column<string>(type: "text", nullable: false),
                    developer_justification = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_disagreement_matrix_row", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_disagreement_matrix_row_vnd_approval_processes_approval",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_disagreement_matrix_row_approval_process_id",
                table: "vnd_disagreement_matrix_row",
                column: "approval_process_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_disagreement_matrix_row");

            migrationBuilder.DropColumn(
                name: "final_hold_comment",
                table: "vnd_approval_stage");

            migrationBuilder.DropColumn(
                name: "final_hold_decided_at",
                table: "vnd_approval_stage");

            migrationBuilder.DropColumn(
                name: "final_hold_decision",
                table: "vnd_approval_stage");
        }
    }
}
