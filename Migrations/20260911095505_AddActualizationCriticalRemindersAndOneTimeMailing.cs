using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddActualizationCriticalRemindersAndOneTimeMailing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "critical_reminder_days_csv",
                table: "vnd_actualization_notification_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "critical_reminders_enabled",
                table: "vnd_actualization_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "vnd_actualization_notification_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "critical_reminder_days_csv", "critical_reminders_enabled" },
                values: new object[] { "", false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "critical_reminder_days_csv",
                table: "vnd_actualization_notification_settings");

            migrationBuilder.DropColumn(
                name: "critical_reminders_enabled",
                table: "vnd_actualization_notification_settings");
        }
    }
}
