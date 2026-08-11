using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzContourImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "requires_paper_sz",
                table: "dictionary_organization_unit",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "audit_entry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_storage_term",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    years = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_storage_term", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numerator",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    scope_key = table.Column<string>(type: "text", nullable: false),
                    pattern = table.Column<string>(type: "text", nullable: false),
                    next_seq = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_numerator", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "route_instance",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    template_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    current_step_order = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_instance", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "route_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_global_rule = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signature",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_attachment_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    stamp_meta = table.Column<string>(type: "text", nullable: true),
                    revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signature", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sz_hr_kind",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_hr_kind", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sz_kind",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    form_key = table.Column<string>(type: "text", nullable: false),
                    is_paper_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    execution_days = table.Column<int>(type: "integer", nullable: false),
                    route_template_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_kind", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_task",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_participant_id = table.Column<int>(type: "integer", nullable: true),
                    assignee_user_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    escalated_to_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_task", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dictionary_nomenclature_case",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    index = table.Column<string>(type: "text", nullable: false),
                    title_ru = table.Column<string>(type: "text", nullable: false),
                    title_en = table.Column<string>(type: "text", nullable: true),
                    title_kg = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true),
                    storage_term_id = table.Column<int>(type: "integer", nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dictionary_nomenclature_case", x => x.id);
                    table.ForeignKey(
                        name: "fk_dictionary_nomenclature_case_organization_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dictionary_nomenclature_case_storage_terms_storage_term_id",
                        column: x => x.storage_term_id,
                        principalTable: "dictionary_storage_term",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_step",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_instance_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_final_methodology = table.Column<bool>(type: "boolean", nullable: false),
                    time_norm_hours = table.Column<int>(type: "integer", nullable: true),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_step", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_step_route_instance_route_instance_id",
                        column: x => x.route_instance_id,
                        principalTable: "route_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "route_template_step",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_template_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_final_methodology = table.Column<bool>(type: "boolean", nullable: false),
                    time_norm_hours = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_template_step", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_template_step_route_template_route_template_id",
                        column: x => x.route_template_id,
                        principalTable: "route_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "text", nullable: false),
                    reg_number = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    current_route_instance_id = table.Column<int>(type: "integer", nullable: true),
                    is_paper_carrier = table.Column<bool>(type: "boolean", nullable: false),
                    nomenclature_case_id = table.Column<int>(type: "integer", nullable: true),
                    storage_term_id = table.Column<int>(type: "integer", nullable: true),
                    archived_on = table.Column<DateOnly>(type: "date", nullable: true),
                    destroy_after_year = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_dictionary_nomenclature_case_nomenclature_case_id",
                        column: x => x.nomenclature_case_id,
                        principalTable: "dictionary_nomenclature_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_dictionary_storage_term_storage_term_id",
                        column: x => x.storage_term_id,
                        principalTable: "dictionary_storage_term",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_users_author_id",
                        column: x => x.author_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_participant",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_step_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    unit_id = table.Column<int>(type: "integer", nullable: true),
                    role_ref = table.Column<string>(type: "text", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_participant_route_steps_route_step_id",
                        column: x => x.route_step_id,
                        principalTable: "route_step",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_route_participant_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_template_participant",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_template_step_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    unit_id = table.Column<int>(type: "integer", nullable: true),
                    role_ref = table.Column<string>(type: "text", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_template_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_template_participant_route_template_steps_route_templ",
                        column: x => x.route_template_step_id,
                        principalTable: "route_template_step",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_attachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    file_ref = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_attachment_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_link",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_document_id = table.Column<int>(type: "integer", nullable: false),
                    to_document_id = table.Column<int>(type: "integer", nullable: false),
                    link_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_link_document_from_document_id",
                        column: x => x.from_document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_link_document_to_document_id",
                        column: x => x.to_document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sz_document",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    kind_id = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    author_unit_id = table.Column<int>(type: "integer", nullable: true),
                    correspondent_unit_id = table.Column<int>(type: "integer", nullable: true),
                    signer_user_id = table.Column<int>(type: "integer", nullable: true),
                    registered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    registered_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    withdraw_reason = table.Column<string>(type: "text", nullable: true),
                    approval_rounds = table.Column<int>(type: "integer", nullable: false),
                    execution_resolution = table.Column<string>(type: "text", nullable: true),
                    execution_resolution_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    execution_resolution_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_date_extension_reason = table.Column<string>(type: "text", nullable: true),
                    due_date_extensions = table.Column<int>(type: "integer", nullable: false),
                    execution_summary = table.Column<string>(type: "text", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    original_holder_user_id = table.Column<int>(type: "integer", nullable: true),
                    original_handed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    original_due_back_on = table.Column<DateOnly>(type: "date", nullable: true),
                    original_location = table.Column<string>(type: "text", nullable: true),
                    original_returned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    original_returned_to_user_id = table.Column<int>(type: "integer", nullable: true),
                    original_handover_count = table.Column<int>(type: "integer", nullable: false),
                    hr_kind_id = table.Column<int>(type: "integer", nullable: true),
                    employee_name = table.Column<string>(type: "text", nullable: true),
                    employee_unit_id = table.Column<int>(type: "integer", nullable: true),
                    transfer_unit_id = table.Column<int>(type: "integer", nullable: true),
                    has_budget = table.Column<bool>(type: "boolean", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    travel_expenses = table.Column<bool>(type: "boolean", nullable: true),
                    extra_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_document_dictionary_organization_unit_author_unit_id",
                        column: x => x.author_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_dictionary_organization_unit_correspondent_unit",
                        column: x => x.correspondent_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_dictionary_organization_unit_employee_unit_id",
                        column: x => x.employee_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_dictionary_organization_unit_transfer_unit_id",
                        column: x => x.transfer_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_document_document_id",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_document_sz_hr_kinds_hr_kind_id",
                        column: x => x.hr_kind_id,
                        principalTable: "sz_hr_kind",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_sz_kinds_kind_id",
                        column: x => x.kind_id,
                        principalTable: "sz_kind",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_users_original_holder_user_id",
                        column: x => x.original_holder_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_document_users_signer_user_id",
                        column: x => x.signer_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resolution",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    route_participant_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    signature_id = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resolution", x => x.id);
                    table.ForeignKey(
                        name: "fk_resolution_route_participants_route_participant_id",
                        column: x => x.route_participant_id,
                        principalTable: "route_participant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sz_assignment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false),
                    assignee_user_id = table.Column<int>(type: "integer", nullable: false),
                    assignee_unit_id = table.Column<int>(type: "integer", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    report_text = table.Column<string>(type: "text", nullable: true),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    return_reason = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_assignment_dictionary_organization_unit_assignee_unit_id",
                        column: x => x.assignee_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_assignment_sz_documents_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_assignment_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sz_document_rubric",
                columns: table => new
                {
                    rubrics_id = table.Column<int>(type: "integer", nullable: false),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_document_rubric", x => new { x.rubrics_id, x.sz_document_id });
                    table.ForeignKey(
                        name: "fk_sz_document_rubric_dictionary_rubric_rubrics_id",
                        column: x => x.rubrics_id,
                        principalTable: "dictionary_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_document_rubric_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "remark",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resolution_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    resolved_confirmed_by_id = table.Column<int>(type: "integer", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_remark", x => x.id);
                    table.ForeignKey(
                        name: "fk_remark_resolutions_resolution_id",
                        column: x => x.resolution_id,
                        principalTable: "resolution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 12,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 15,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 18,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 19,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 20,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 21,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 22,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 23,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 24,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 25,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 27,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 28,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 29,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 30,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 32,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 34,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 37,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 39,
                column: "requires_paper_sz",
                value: false);

            migrationBuilder.InsertData(
                table: "dictionary_storage_term",
                columns: new[] { "id", "code", "created_at", "is_active", "title_en", "title_kg", "title_ru", "updated_at", "years" },
                values: new object[,]
                {
                    { 1, "1г", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "1 year", "1 жыл", "1 год", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, "3г", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "3 years", "3 жыл", "3 года", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3 },
                    { 3, "5л", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "5 years", "5 жыл", "5 лет", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5 },
                    { 4, "10л", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "10 years", "10 жыл", "10 лет", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10 },
                    { 5, "75л", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "75 years (personnel)", "75 жыл (кадрлар боюнча)", "75 лет (по личному составу)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 75 },
                    { 6, "Пост", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Permanent", "Туруктуу", "Постоянно", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "sz_hr_kind",
                columns: new[] { "id", "created_at", "is_active", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Salary change", "Эмгек акыны өзгөртүү", "Изменение оклада", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Business trip", "Иш сапар", "Командировка", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Transfer with salary change", "Эмгек акы өзгөрүү менен которуу", "Перемещение с изменением оклада", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Transfer without salary change", "Эмгек акы өзгөрбөй которуу", "Перемещение без изменения оклада", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Hiring", "Жумушка кабыл алуу", "Приём на работу", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Paid internship", "Акы төлөнүүчү стажировка", "Приём стажёров с оплатой", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Unpaid internship", "Акысыз стажировка", "Приём стажёров без оплаты", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Work on day off", "Эс алуу күнү иштөө", "Работа в выходной день", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Other", "Башка", "Другое", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "sz_kind",
                columns: new[] { "id", "created_at", "execution_days", "form_key", "is_active", "is_paper_by_default", "route_template_id", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 14, "Hr", true, true, null, "HR", "Кадрдык", "Кадровая", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 14, "Procurement", true, false, null, "Procurement", "Сатып алууга", "На закупку", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 14, "Training", true, false, null, "Training", "Окутууга", "На обучение", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 14, "Other", true, false, null, "Other", "Башка", "Прочие", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "dictionary_nomenclature_case",
                columns: new[] { "id", "closed_on", "created_at", "index", "is_active", "org_unit_id", "storage_term_id", "title_en", "title_kg", "title_ru", "updated_at", "year" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "05-01", true, 1, 3, "Memos on core activities", "Негизги ишмердүүлүк боюнча кызматтык каттар", "Служебные записки по основной деятельности", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2026 },
                    { 2, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "05-02", true, 1, 5, "Memos on personnel", "Кадрлар боюнча кызматтык каттар", "Служебные записки по личному составу", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2026 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_at",
                table: "audit_entry",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_entity_type_entity_id",
                table: "audit_entry",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_nomenclature_case_org_unit_id",
                table: "dictionary_nomenclature_case",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_nomenclature_case_storage_term_id",
                table: "dictionary_nomenclature_case",
                column: "storage_term_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_nomenclature_case_year_index",
                table: "dictionary_nomenclature_case",
                columns: new[] { "year", "index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_storage_term_code",
                table: "dictionary_storage_term",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_author_id",
                table: "document",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_destroy_after_year",
                table: "document",
                column: "destroy_after_year");

            migrationBuilder.CreateIndex(
                name: "ix_document_nomenclature_case_id",
                table: "document",
                column: "nomenclature_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_status_code",
                table: "document",
                column: "status_code");

            migrationBuilder.CreateIndex(
                name: "ix_document_storage_term_id",
                table: "document",
                column: "storage_term_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_type_reg_number",
                table: "document",
                columns: new[] { "type", "reg_number" });

            migrationBuilder.CreateIndex(
                name: "ix_document_attachment_document_id",
                table: "document_attachment",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_attachment_hash",
                table: "document_attachment",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_document_link_from_document_id_to_document_id_link_type",
                table: "document_link",
                columns: new[] { "from_document_id", "to_document_id", "link_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_link_to_document_id",
                table: "document_link",
                column: "to_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_numerator_document_type_scope_scope_key",
                table: "numerator",
                columns: new[] { "document_type", "scope", "scope_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_remark_resolution_id",
                table: "remark",
                column: "resolution_id");

            migrationBuilder.CreateIndex(
                name: "ix_resolution_route_participant_id",
                table: "resolution",
                column: "route_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_route_instance_document_id",
                table: "route_instance",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_participant_route_step_id",
                table: "route_participant",
                column: "route_step_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_participant_user_id",
                table: "route_participant",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_step_route_instance_id_order",
                table: "route_step",
                columns: new[] { "route_instance_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_route_template_participant_route_template_step_id",
                table: "route_template_participant",
                column: "route_template_step_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_template_step_route_template_id",
                table: "route_template_step",
                column: "route_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_signature_document_attachment_id",
                table: "signature",
                column: "document_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_assignment_assignee_unit_id",
                table: "sz_assignment",
                column: "assignee_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_assignment_assignee_user_id_state",
                table: "sz_assignment",
                columns: new[] { "assignee_user_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_sz_assignment_due_date",
                table: "sz_assignment",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_sz_assignment_sz_document_id",
                table: "sz_assignment",
                column: "sz_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_author_unit_id",
                table: "sz_document",
                column: "author_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_correspondent_unit_id",
                table: "sz_document",
                column: "correspondent_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_document_id",
                table: "sz_document",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_due_date",
                table: "sz_document",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_employee_unit_id",
                table: "sz_document",
                column: "employee_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_hr_kind_id",
                table: "sz_document",
                column: "hr_kind_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_kind_id",
                table: "sz_document",
                column: "kind_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_original_holder_user_id_original_returned_at",
                table: "sz_document",
                columns: new[] { "original_holder_user_id", "original_returned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_signer_user_id",
                table: "sz_document",
                column: "signer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_transfer_unit_id",
                table: "sz_document",
                column: "transfer_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_rubric_sz_document_id",
                table: "sz_document_rubric",
                column: "sz_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_task_assignee_user_id_state",
                table: "workflow_task",
                columns: new[] { "assignee_user_id", "state" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry");

            migrationBuilder.DropTable(
                name: "document_attachment");

            migrationBuilder.DropTable(
                name: "document_link");

            migrationBuilder.DropTable(
                name: "numerator");

            migrationBuilder.DropTable(
                name: "remark");

            migrationBuilder.DropTable(
                name: "route_template_participant");

            migrationBuilder.DropTable(
                name: "signature");

            migrationBuilder.DropTable(
                name: "sz_assignment");

            migrationBuilder.DropTable(
                name: "sz_document_rubric");

            migrationBuilder.DropTable(
                name: "workflow_task");

            migrationBuilder.DropTable(
                name: "resolution");

            migrationBuilder.DropTable(
                name: "route_template_step");

            migrationBuilder.DropTable(
                name: "sz_document");

            migrationBuilder.DropTable(
                name: "route_participant");

            migrationBuilder.DropTable(
                name: "route_template");

            migrationBuilder.DropTable(
                name: "document");

            migrationBuilder.DropTable(
                name: "sz_hr_kind");

            migrationBuilder.DropTable(
                name: "sz_kind");

            migrationBuilder.DropTable(
                name: "route_step");

            migrationBuilder.DropTable(
                name: "dictionary_nomenclature_case");

            migrationBuilder.DropTable(
                name: "route_instance");

            migrationBuilder.DropTable(
                name: "dictionary_storage_term");

            migrationBuilder.DropColumn(
                name: "requires_paper_sz",
                table: "dictionary_organization_unit");
        }
    }
}
