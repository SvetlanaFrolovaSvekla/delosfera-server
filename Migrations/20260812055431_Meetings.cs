using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class Meetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    body = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    form = table.Column<int>(type: "integer", nullable: false),
                    secretary_user_id = table.Column<int>(type: "integer", nullable: false),
                    secretary_unit_id = table.Column<int>(type: "integer", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    materials_url = table.Column<string>(type: "text", nullable: true),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_dictionary_organization_unit_secretary_unit_id",
                        column: x => x.secretary_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_users_secretary_user_id",
                        column: x => x.secretary_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meeting_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    protocol_number = table.Column<string>(type: "text", nullable: true),
                    protocol_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision = table.Column<string>(type: "text", nullable: true),
                    speaker_user_id = table.Column<int>(type: "integer", nullable: true),
                    speaker_head_user_id = table.Column<int>(type: "integer", nullable: true),
                    speaker_unit_id = table.Column<int>(type: "integer", nullable: true),
                    deputy_secretary_user_id = table.Column<int>(type: "integer", nullable: true),
                    controller_user_id = table.Column<int>(type: "integer", nullable: true),
                    documents_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_agenda_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_dictionary_organization_unit_speaker_un",
                        column: x => x.speaker_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meeting",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_users_controller_user_id",
                        column: x => x.controller_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_users_deputy_secretary_user_id",
                        column: x => x.deputy_secretary_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_users_speaker_head_user_id",
                        column: x => x.speaker_head_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_users_speaker_user_id",
                        column: x => x.speaker_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_assignment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agenda_item_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    report = table.Column<string>(type: "text", nullable: true),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reported_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    last_reminder_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_agenda_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_assignment_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "meeting_agenda_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_assignment_dictionary_organization_unit_org_",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_assignment_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_file",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agenda_item_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_agenda_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_file_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "meeting_agenda_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_file_file_attachments_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_guest",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agenda_item_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_agenda_guest", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_guest_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "meeting_agenda_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_guest_dictionary_organization_unit_org_unit_",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_agenda_guest_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_meeting_secretary_unit_id",
                table: "meeting",
                column: "secretary_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_secretary_user_id",
                table: "meeting",
                column: "secretary_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_year_body_number",
                table: "meeting",
                columns: new[] { "year", "body", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_assignment_agenda_item_id",
                table: "meeting_agenda_assignment",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_assignment_due_date_status",
                table: "meeting_agenda_assignment",
                columns: new[] { "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_assignment_org_unit_id",
                table: "meeting_agenda_assignment",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_assignment_user_id",
                table: "meeting_agenda_assignment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_file_agenda_item_id_kind",
                table: "meeting_agenda_file",
                columns: new[] { "agenda_item_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_file_file_id",
                table: "meeting_agenda_file",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_guest_agenda_item_id_user_id",
                table: "meeting_agenda_guest",
                columns: new[] { "agenda_item_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_guest_org_unit_id",
                table: "meeting_agenda_guest",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_guest_user_id",
                table: "meeting_agenda_guest",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_controller_user_id",
                table: "meeting_agenda_item",
                column: "controller_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_deputy_secretary_user_id",
                table: "meeting_agenda_item",
                column: "deputy_secretary_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_meeting_id_order",
                table: "meeting_agenda_item",
                columns: new[] { "meeting_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_speaker_head_user_id",
                table: "meeting_agenda_item",
                column: "speaker_head_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_speaker_unit_id",
                table: "meeting_agenda_item",
                column: "speaker_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_speaker_user_id",
                table: "meeting_agenda_item",
                column: "speaker_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_agenda_assignment");

            migrationBuilder.DropTable(
                name: "meeting_agenda_file");

            migrationBuilder.DropTable(
                name: "meeting_agenda_guest");

            migrationBuilder.DropTable(
                name: "meeting_agenda_item");

            migrationBuilder.DropTable(
                name: "meeting");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });
        }
    }
}
