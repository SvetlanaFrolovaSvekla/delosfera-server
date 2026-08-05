using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenameApprovalDeadlinesToMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "repeat_deadline_hours",
                table: "vnd_approval_process",
                newName: "repeat_deadline_minutes");

            migrationBuilder.RenameColumn(
                name: "primary_deadline_hours",
                table: "vnd_approval_process",
                newName: "primary_deadline_minutes");

            migrationBuilder.RenameColumn(
                name: "final_hold_deadline_hours",
                table: "vnd_approval_process",
                newName: "final_hold_deadline_minutes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "repeat_deadline_minutes",
                table: "vnd_approval_process",
                newName: "repeat_deadline_hours");

            migrationBuilder.RenameColumn(
                name: "primary_deadline_minutes",
                table: "vnd_approval_process",
                newName: "primary_deadline_hours");

            migrationBuilder.RenameColumn(
                name: "final_hold_deadline_minutes",
                table: "vnd_approval_process",
                newName: "final_hold_deadline_hours");
        }
    }
}
