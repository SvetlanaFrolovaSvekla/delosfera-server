using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class PoaObligationsCorrespondenceSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "submit_to_body",
                table: "sz_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submit_to_body_question",
                table: "sz_document",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "submit_to_body_requested_at",
                table: "sz_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "submit_to_body_requested_by_user_id",
                table: "sz_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_sz_id",
                table: "meeting_agenda_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "vnd_document",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(title_ru, '') || ' ' || coalesce(code, '') || ' ' || coalesce(title_kg, ''))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "correspondent",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    short_title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    tax_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contact_person = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correspondent", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "power_of_attorney",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reg_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    grantor_user_id = table.Column<int>(type: "integer", nullable: false),
                    parent_poa_id = table.Column<int>(type: "integer", nullable: true),
                    holder_kind = table.Column<int>(type: "integer", nullable: false),
                    holder_user_id = table.Column<int>(type: "integer", nullable: true),
                    holder_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    holder_position = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    holder_unit_id = table.Column<int>(type: "integer", nullable: true),
                    holder_identity_document = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    powers = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian', coalesce(holder_name, '') || ' ' || coalesce(powers, '') || ' ' || coalesce(reg_number, ''))", stored: true),
                    allows_delegation = table.Column<bool>(type: "boolean", nullable: false),
                    amount_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    signed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    revoked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revoked_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    original_location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    original_handed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    original_returned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_power_of_attorney", x => x.id);
                    table.ForeignKey(
                        name: "fk_power_of_attorney_dictionary_organization_unit_holder_unit_",
                        column: x => x.holder_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_power_of_attorney_power_of_attorney_parent_poa_id",
                        column: x => x.parent_poa_id,
                        principalTable: "power_of_attorney",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_power_of_attorney_users_grantor_user_id",
                        column: x => x.grantor_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_power_of_attorney_users_holder_user_id",
                        column: x => x.holder_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_power_of_attorney_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recurring_obligation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    basis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    periodicity = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<int>(type: "integer", nullable: true),
                    responsible_user_id = table.Column<int>(type: "integer", nullable: true),
                    responsible_unit_id = table.Column<int>(type: "integer", nullable: true),
                    grace_days = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_obligation", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_obligation_dictionary_organization_unit_responsib",
                        column: x => x.responsible_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recurring_obligation_users_responsible_user_id",
                        column: x => x.responsible_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "correspondence_letter",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    reg_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    registered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    registered_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    correspondent_id = table.Column<int>(type: "integer", nullable: false),
                    their_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    their_date = table.Column<DateOnly>(type: "date", nullable: true),
                    subject = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian', coalesce(subject, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(reg_number, '') || ' ' || coalesce(their_number, ''))", stored: true),
                    delivery_method = table.Column<int>(type: "integer", nullable: false),
                    sheet_count = table.Column<int>(type: "integer", nullable: true),
                    enclosures = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolution_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    responsible_user_id = table.Column<int>(type: "integer", nullable: true),
                    responsible_unit_id = table.Column<int>(type: "integer", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_controlled = table.Column<bool>(type: "boolean", nullable: false),
                    execution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    in_reply_to_id = table.Column<int>(type: "integer", nullable: true),
                    source_sz_id = table.Column<int>(type: "integer", nullable: true),
                    nomenclature_case_id = table.Column<int>(type: "integer", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correspondence_letter", x => x.id);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_correspondence_letter_in_reply_to_id",
                        column: x => x.in_reply_to_id,
                        principalTable: "correspondence_letter",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_correspondents_correspondent_id",
                        column: x => x.correspondent_id,
                        principalTable: "correspondent",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_nomenclature_cases_nomenclature_case_",
                        column: x => x.nomenclature_case_id,
                        principalTable: "dictionary_nomenclature_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_organization_units_responsible_unit_id",
                        column: x => x.responsible_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_users_resolution_by_user_id",
                        column: x => x.resolution_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_correspondence_letter_users_responsible_user_id",
                        column: x => x.responsible_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "poa_file",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    power_of_attorney_id = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poa_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_poa_file_file_attachments_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_poa_file_powers_of_attorney_power_of_attorney_id",
                        column: x => x.power_of_attorney_id,
                        principalTable: "power_of_attorney",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "obligation_period",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    obligation_id = table.Column<int>(type: "integer", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fulfilled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fulfilled_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    meeting_id = table.Column<int>(type: "integer", nullable: true),
                    document_id = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obligation_period", x => x.id);
                    table.ForeignKey(
                        name: "fk_obligation_period_meeting_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meeting",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_obligation_period_recurring_obligations_obligation_id",
                        column: x => x.obligation_id,
                        principalTable: "recurring_obligation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_obligation_period_users_fulfilled_by_user_id",
                        column: x => x.fulfilled_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "letter_file",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    letter_id = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_letter_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_letter_file_correspondence_letter_letter_id",
                        column: x => x.letter_id,
                        principalTable: "correspondence_letter",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_letter_file_file_attachments_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_search_vector",
                table: "vnd_document",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_submit_to_body_requested_by_user_id",
                table: "sz_document",
                column: "submit_to_body_requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_source_sz_id",
                table: "meeting_agenda_item",
                column: "source_sz_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_category_status",
                table: "correspondence_letter",
                columns: new[] { "category", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_correspondent_id",
                table: "correspondence_letter",
                column: "correspondent_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_direction_registered_on",
                table: "correspondence_letter",
                columns: new[] { "direction", "registered_on" });

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_direction_year_reg_number",
                table: "correspondence_letter",
                columns: new[] { "direction", "year", "reg_number" },
                unique: true,
                filter: "reg_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_in_reply_to_id",
                table: "correspondence_letter",
                column: "in_reply_to_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_nomenclature_case_id",
                table: "correspondence_letter",
                column: "nomenclature_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_resolution_by_user_id",
                table: "correspondence_letter",
                column: "resolution_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_responsible_unit_id",
                table: "correspondence_letter",
                column: "responsible_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_responsible_user_id",
                table: "correspondence_letter",
                column: "responsible_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_search_vector",
                table: "correspondence_letter",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_correspondence_letter_status_due_date",
                table: "correspondence_letter",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_correspondent_kind_is_active",
                table: "correspondent",
                columns: new[] { "kind", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_correspondent_title",
                table: "correspondent",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_letter_file_file_id",
                table: "letter_file",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_letter_file_letter_id",
                table: "letter_file",
                column: "letter_id");

            migrationBuilder.CreateIndex(
                name: "ix_obligation_period_fulfilled_by_user_id",
                table: "obligation_period",
                column: "fulfilled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_obligation_period_meeting_id",
                table: "obligation_period",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_obligation_period_obligation_id_period_start",
                table: "obligation_period",
                columns: new[] { "obligation_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_obligation_period_status_due_date",
                table: "obligation_period",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_poa_file_file_id",
                table: "poa_file",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_poa_file_power_of_attorney_id",
                table: "poa_file",
                column: "power_of_attorney_id");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_grantor_user_id",
                table: "power_of_attorney",
                column: "grantor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_holder_unit_id",
                table: "power_of_attorney",
                column: "holder_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_holder_user_id_status",
                table: "power_of_attorney",
                columns: new[] { "holder_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_parent_poa_id",
                table: "power_of_attorney",
                column: "parent_poa_id");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_revoked_by_user_id",
                table: "power_of_attorney",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_search_vector",
                table: "power_of_attorney",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_status_valid_to",
                table: "power_of_attorney",
                columns: new[] { "status", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_power_of_attorney_year_reg_number",
                table: "power_of_attorney",
                columns: new[] { "year", "reg_number" },
                unique: true,
                filter: "reg_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_obligation_is_active_kind",
                table: "recurring_obligation",
                columns: new[] { "is_active", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_obligation_responsible_unit_id",
                table: "recurring_obligation",
                column: "responsible_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_obligation_responsible_user_id",
                table: "recurring_obligation",
                column: "responsible_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_agenda_item_sz_documents_source_sz_id",
                table: "meeting_agenda_item",
                column: "source_sz_id",
                principalTable: "sz_document",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_sz_document_users_submit_to_body_requested_by_user_id",
                table: "sz_document",
                column: "submit_to_body_requested_by_user_id",
                principalTable: "user",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_agenda_item_sz_documents_source_sz_id",
                table: "meeting_agenda_item");

            migrationBuilder.DropForeignKey(
                name: "fk_sz_document_users_submit_to_body_requested_by_user_id",
                table: "sz_document");

            migrationBuilder.DropTable(
                name: "letter_file");

            migrationBuilder.DropTable(
                name: "obligation_period");

            migrationBuilder.DropTable(
                name: "poa_file");

            migrationBuilder.DropTable(
                name: "correspondence_letter");

            migrationBuilder.DropTable(
                name: "recurring_obligation");

            migrationBuilder.DropTable(
                name: "power_of_attorney");

            migrationBuilder.DropTable(
                name: "correspondent");

            migrationBuilder.DropIndex(
                name: "ix_vnd_document_search_vector",
                table: "vnd_document");

            migrationBuilder.DropIndex(
                name: "ix_sz_document_submit_to_body_requested_by_user_id",
                table: "sz_document");

            migrationBuilder.DropIndex(
                name: "ix_meeting_agenda_item_source_sz_id",
                table: "meeting_agenda_item");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "submit_to_body",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "submit_to_body_question",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "submit_to_body_requested_at",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "submit_to_body_requested_by_user_id",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "source_sz_id",
                table: "meeting_agenda_item");

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
        }
    }
}
