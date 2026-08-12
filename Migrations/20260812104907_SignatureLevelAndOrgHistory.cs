using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SignatureLevelAndOrgHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "required_signature_level",
                table: "route_template_step",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "required_signature_level",
                table: "route_step",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dictionary_organization_unit_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    org_unit_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_organization_unit_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_organization_unit_history_dictionary_organizatio",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dictionary_organization_unit_history_users_changed_by_user_",
                        column: x => x.changed_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_organization_unit_history_changed_by_user_id",
                table: "dictionary_organization_unit_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_organization_unit_history_effective_from",
                table: "dictionary_organization_unit_history",
                column: "effective_from");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_organization_unit_history_org_unit_id_effective_",
                table: "dictionary_organization_unit_history",
                columns: new[] { "org_unit_id", "effective_from" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dictionary_organization_unit_history");

            migrationBuilder.DropColumn(
                name: "required_signature_level",
                table: "route_template_step");

            migrationBuilder.DropColumn(
                name: "required_signature_level",
                table: "route_step");
        }
    }
}
