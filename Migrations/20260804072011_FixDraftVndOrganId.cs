using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class FixDraftVndOrganId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "vnd_document",
                columns: new[] { "id", "adoption_code", "adoption_date", "archived_date", "cancel_code", "cancel_date", "cancel_reason", "code", "created_at", "curator_developer_id", "current_redaction_id", "developer_id", "due_actualization_date", "effective_date", "last_actualization_date", "last_actualization_had_changes", "organ_id", "requisites_changed_date", "revision_changed_date", "secrecy_level_id", "status", "title_en", "title_kg", "title_ru", "type_id", "updated_at" },
                values: new object[] { 6, null, null, null, null, null, null, "10038", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, null, 3, null, null, null, false, 2, null, null, 1, 5, null, null, "Тестовый черновик", 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }
    }
}
