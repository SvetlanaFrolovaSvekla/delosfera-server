using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddActualizationNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "attachment_bytes",
                table: "outgoing_email",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_content_type",
                table: "outgoing_email",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_file_name",
                table: "outgoing_email",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vnd_actualization_notification_responsible",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    org_unit_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_notification_responsible", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_notification_responsible_dictionary_organ",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_notification_responsible_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_actualization_notification_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    monthly_digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    monthly_digest_columns_csv = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_notification_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "vnd_actualization_notification_settings",
                columns: new[] { "id", "created_at", "monthly_digest_columns_csv", "monthly_digest_enabled", "updated_at" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_notification_responsible_org_unit_id_user",
                table: "vnd_actualization_notification_responsible",
                columns: new[] { "org_unit_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_notification_responsible_user_id",
                table: "vnd_actualization_notification_responsible",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_actualization_notification_responsible");

            migrationBuilder.DropTable(
                name: "vnd_actualization_notification_settings");

            migrationBuilder.DropColumn(
                name: "attachment_bytes",
                table: "outgoing_email");

            migrationBuilder.DropColumn(
                name: "attachment_content_type",
                table: "outgoing_email");

            migrationBuilder.DropColumn(
                name: "attachment_file_name",
                table: "outgoing_email");
        }
    }
}
