using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class OrgStructureIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "external_id",
                table: "dictionary_organization_unit",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "org_structure_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    portal_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    token_encrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    create_missing_units = table.Column<bool>(type: "boolean", nullable: false),
                    match_by_email = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_structure_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_sync_run",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    started_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    units_received = table.Column<int>(type: "integer", nullable: false),
                    units_created = table.Column<int>(type: "integer", nullable: false),
                    units_updated = table.Column<int>(type: "integer", nullable: false),
                    units_skipped = table.Column<int>(type: "integer", nullable: false),
                    employees_received = table.Column<int>(type: "integer", nullable: false),
                    employees_matched = table.Column<int>(type: "integer", nullable: false),
                    employees_unmatched = table.Column<int>(type: "integer", nullable: false),
                    employees_updated = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    notes_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_sync_run", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 12,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 15,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 18,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 19,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 20,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 21,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 22,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 23,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 24,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 25,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 27,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 28,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 29,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 30,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 32,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 34,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 37,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                column: "external_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 39,
                column: "external_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_org_sync_run_started_at",
                table: "org_sync_run",
                column: "started_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "org_structure_settings");

            migrationBuilder.DropTable(
                name: "org_sync_run");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "dictionary_organization_unit");
        }
    }
}
