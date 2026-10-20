using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Этап «На подписании» заявки на замещение: зафиксированные версии приказа и договора о
    /// матответственности (substitution_documents) и журнал подписей ЭЦП ГП «Инфоком» под ними
    /// (substitution_signatures). Плюс примечание о пропущенных этапах маршрута (substitution_requests.route_note)
    /// и подписант от Банка для Головного офиса (hr_routing_settings.head_office_signer_user_id).
    /// Миграция без Designer-файла, как и остальные последние миграции проекта; снапшот модели обновлён.
    ///
    /// Как у журналов ВНД и СЗ, триггер запрещает UPDATE/DELETE строк журнала подписей.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261020090000_AddSubstitutionSigning")]
    public partial class AddSubstitutionSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "route_note",
                table: "substitution_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "head_office_signer_user_id",
                table: "hr_routing_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "substitution_documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    director_user_id = table.Column<int>(type: "integer", nullable: true),
                    absent_user_id = table.Column<int>(type: "integer", nullable: true),
                    substitute_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitution_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitution_documents_substitution_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "substitution_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "substitution_signatures",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    on_behalf_of_user_id = table.Column<int>(type: "integer", nullable: true),
                    person_idnp = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    person_fio = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    organization_inn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    organization_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_key_identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cert_common_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cert_valid_not_before = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    cert_valid_not_after = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    cert_issuer_common_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cert_issuer_organization_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hash_schema_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signature_base64 = table.Column<string>(type: "text", nullable: false),
                    signature_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cms_signing_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_check_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified = table.Column<bool>(type: "boolean", nullable: false),
                    verification_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitution_signatures", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitution_signatures_substitution_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "substitution_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_signatures_substitution_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "substitution_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_signatures_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_substitution_documents_request_id_kind_version",
                table: "substitution_documents",
                columns: new[] { "request_id", "kind", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_substitution_signatures_document_id",
                table: "substitution_signatures",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_signatures_request_id_document_id",
                table: "substitution_signatures",
                columns: new[] { "request_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_substitution_signatures_signature_sha256",
                table: "substitution_signatures",
                column: "signature_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_substitution_signatures_user_id_person_idnp",
                table: "substitution_signatures",
                columns: new[] { "user_id", "person_idnp" });

            // Append-only на уровне БД.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION substitution_signatures_block_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'substitution_signatures is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_substitution_signatures_append_only
                BEFORE UPDATE OR DELETE ON substitution_signatures
                FOR EACH ROW EXECUTE FUNCTION substitution_signatures_block_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_substitution_signatures_append_only ON substitution_signatures;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS substitution_signatures_block_changes();");

            migrationBuilder.DropTable(
                name: "substitution_signatures");

            migrationBuilder.DropTable(
                name: "substitution_documents");

            migrationBuilder.DropColumn(
                name: "route_note",
                table: "substitution_requests");

            migrationBuilder.DropColumn(
                name: "head_office_signer_user_id",
                table: "hr_routing_settings");
        }
    }
}
