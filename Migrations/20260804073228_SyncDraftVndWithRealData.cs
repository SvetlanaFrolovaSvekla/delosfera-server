using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SyncDraftVndWithRealData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "revision_changed_date",
                value: null);

            migrationBuilder.InsertData(
                table: "vnd_document",
                columns: new[] { "id", "adoption_code", "adoption_date", "archived_date", "cancel_code", "cancel_date", "cancel_reason", "code", "created_at", "curator_developer_id", "current_redaction_id", "developer_id", "due_actualization_date", "effective_date", "last_actualization_date", "last_actualization_had_changes", "organ_id", "requisites_changed_date", "revision_changed_date", "secrecy_level_id", "status", "title_en", "title_kg", "title_ru", "type_id", "updated_at" },
                values: new object[] { 6, null, null, null, null, null, null, "10210", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 33, new DateOnly(2027, 8, 4), null, new DateOnly(2026, 8, 4), false, 2, null, null, 3, 5, null, null, "Тест", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "revision_changed_date",
                value: new DateOnly(2019, 2, 1));
        }
    }
}
