using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalSheetFileIdToVndRedaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "approval_sheet_file_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_approval_sheet_file_id",
                table: "vnd_redaction",
                column: "approval_sheet_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_file_attachments_approval_sheet_file_id",
                table: "vnd_redaction",
                column: "approval_sheet_file_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_file_attachments_approval_sheet_file_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_approval_sheet_file_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "approval_sheet_file_id",
                table: "vnd_redaction");
        }
    }
}
