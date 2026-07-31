using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddRepeatInitiatorCommentToApprovalProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "repeat_initiator_comment",
                table: "vnd_approval_stage",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repeat_initiator_comment",
                table: "vnd_approval_process",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "repeat_initiator_comment",
                table: "vnd_approval_stage");

            migrationBuilder.DropColumn(
                name: "repeat_initiator_comment",
                table: "vnd_approval_process");
        }
    }
}
