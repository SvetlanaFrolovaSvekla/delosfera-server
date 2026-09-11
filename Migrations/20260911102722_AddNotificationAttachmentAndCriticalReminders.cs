using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAttachmentAndCriticalReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attachment_file_id",
                table: "notification",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_attachment_file_id",
                table: "notification",
                column: "attachment_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notification_file_attachments_attachment_file_id",
                table: "notification",
                column: "attachment_file_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_file_attachments_attachment_file_id",
                table: "notification");

            migrationBuilder.DropIndex(
                name: "ix_notification_attachment_file_id",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "attachment_file_id",
                table: "notification");
        }
    }
}
