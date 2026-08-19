using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class DirectorySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "directory_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    server = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    service_account_login = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    service_account_password_encrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    users_base_dn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    users_filter = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    page_size = table.Column<int>(type: "integer", nullable: false),
                    login_attribute = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email_attribute = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    full_name_attribute = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    default_role_id = table.Column<int>(type: "integer", nullable: true),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_created = table.Column<int>(type: "integer", nullable: false),
                    last_sync_updated = table.Column<int>(type: "integer", nullable: false),
                    last_sync_deactivated = table.Column<int>(type: "integer", nullable: false),
                    last_sync_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_settings", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 });

            // Право на системные настройки выдаётся роли администратора: иначе раздел
            // создан, но открыть его некому — даже тому, кто настраивает систему.
            migrationBuilder.Sql(@"
                UPDATE role
                SET permission_codes = permission_codes || 35
                WHERE title_ru = 'Администратор'
                  AND NOT (permission_codes @> ARRAY[35]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "directory_settings");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 });
        }
    }
}
