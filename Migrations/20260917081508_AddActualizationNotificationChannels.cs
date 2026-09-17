using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddActualizationNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "critical_reminders_notify_email",
                table: "vnd_actualization_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "critical_reminders_notify_in_app",
                table: "vnd_actualization_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "monthly_digest_notify_email",
                table: "vnd_actualization_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "monthly_digest_notify_in_app",
                table: "vnd_actualization_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 });

            migrationBuilder.UpdateData(
                table: "vnd_actualization_notification_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "critical_reminders_notify_email", "critical_reminders_notify_in_app", "monthly_digest_notify_email", "monthly_digest_notify_in_app" },
                values: new object[] { true, true, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "critical_reminders_notify_email",
                table: "vnd_actualization_notification_settings");

            migrationBuilder.DropColumn(
                name: "critical_reminders_notify_in_app",
                table: "vnd_actualization_notification_settings");

            migrationBuilder.DropColumn(
                name: "monthly_digest_notify_email",
                table: "vnd_actualization_notification_settings");

            migrationBuilder.DropColumn(
                name: "monthly_digest_notify_in_app",
                table: "vnd_actualization_notification_settings");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56 });
        }
    }
}
