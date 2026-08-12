using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ActualizationPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_actualization_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approval_note = table.Column<string>(type: "text", nullable: true),
                    approved_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_plan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vnd_actualization_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    green_threshold_days = table.Column<int>(type: "integer", nullable: false),
                    red_threshold_days = table.Column<int>(type: "integer", nullable: false),
                    critical_reminder_days = table.Column<int>(type: "integer", nullable: false),
                    monthly_digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vnd_actualization_plan_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    vnd_document_id = table.Column<int>(type: "integer", nullable: true),
                    responsible_unit_id = table.Column<int>(type: "integer", nullable: true),
                    curator_user_id = table.Column<int>(type: "integer", nullable: true),
                    approval_body_id = table.Column<int>(type: "integer", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    started_on = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_plan_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_dictionary_approval_body_approv",
                        column: x => x.approval_body_id,
                        principalTable: "dictionary_approval_body",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_dictionary_organization_unit_re",
                        column: x => x.responsible_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_users_curator_user_id",
                        column: x => x.curator_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_vnd_actualization_plan_plan_id",
                        column: x => x.plan_id,
                        principalTable: "vnd_actualization_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_vnd_documents_vnd_document_id",
                        column: x => x.vnd_document_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vnd_actualization_plan_item_event",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_item_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_plan_item_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_event_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_plan_item_event_vnd_actualization_plan_it",
                        column: x => x.plan_item_id,
                        principalTable: "vnd_actualization_plan_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "vnd_actualization_settings",
                columns: new[] { "id", "created_at", "critical_reminder_days", "green_threshold_days", "monthly_digest_enabled", "red_threshold_days", "updated_at" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, 30, true, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_year",
                table: "vnd_actualization_plan",
                column: "year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_approval_body_id",
                table: "vnd_actualization_plan_item",
                column: "approval_body_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_curator_user_id",
                table: "vnd_actualization_plan_item",
                column: "curator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_due_date_status",
                table: "vnd_actualization_plan_item",
                columns: new[] { "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_plan_id",
                table: "vnd_actualization_plan_item",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_responsible_unit_id",
                table: "vnd_actualization_plan_item",
                column: "responsible_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_vnd_document_id",
                table: "vnd_actualization_plan_item",
                column: "vnd_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_event_plan_item_id_at",
                table: "vnd_actualization_plan_item_event",
                columns: new[] { "plan_item_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_plan_item_event_user_id",
                table: "vnd_actualization_plan_item_event",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_actualization_plan_item_event");

            migrationBuilder.DropTable(
                name: "vnd_actualization_settings");

            migrationBuilder.DropTable(
                name: "vnd_actualization_plan_item");

            migrationBuilder.DropTable(
                name: "vnd_actualization_plan");
        }
    }
}
