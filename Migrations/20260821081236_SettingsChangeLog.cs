using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SettingsChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settings_change",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    entity_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    changes_json = table.Column<string>(type: "jsonb", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    user_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings_change", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_area_at",
                table: "settings_change",
                columns: new[] { "area", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_at",
                table: "settings_change",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_entity_type_entity_id",
                table: "settings_change",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settings_change");
        }
    }
}
