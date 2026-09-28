using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Журнал подписей ЭЦП ГП «Инфоком» под резолюциями "Согласовать" (SIG-ECP) — см.
    /// VndResolutionSignature. Миграция написана вручную (без Designer-файла, как и остальные
    /// последние миграции проекта), снапшот модели обновлён.
    ///
    /// Кроме таблицы создаётся триггер, запрещающий UPDATE/DELETE строк: журнал неизменяем даже
    /// для того, кто работает с БД в обход приложения (TRUNCATE/DROP триггер не останавливает —
    /// это операции уровня администратора БД, а не приложения).</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260928090000_AddVndResolutionSignatures")]
    public partial class AddVndResolutionSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_resolution_signature",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    approval_process_id = table.Column<int>(type: "integer", nullable: false),
                    redaction_id = table.Column<int>(type: "integer", nullable: false),
                    stage_id = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
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
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    comment_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_digest = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    vnd_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_vnd_resolution_signature", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_resolution_signature_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_resolution_signature_vnd_approval_process_id",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_resolution_signature_vnd_redaction_redaction_id",
                        column: x => x.redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_resolution_signature_vnd_approval_stage_stage_id",
                        column: x => x.stage_id,
                        principalTable: "vnd_approval_stage",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_resolution_signature_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_resolution_signature_approval_process_id",
                table: "vnd_resolution_signature",
                column: "approval_process_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_resolution_signature_person_idnp",
                table: "vnd_resolution_signature",
                column: "person_idnp");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_resolution_signature_redaction_id",
                table: "vnd_resolution_signature",
                column: "redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_resolution_signature_user_id",
                table: "vnd_resolution_signature",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_resolution_signature_vnd_id",
                table: "vnd_resolution_signature",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ux_vnd_resolution_signature_resolution_user",
                table: "vnd_resolution_signature",
                columns: new[] { "stage_id", "phase", "round_number", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_vnd_resolution_signature_signature_sha256",
                table: "vnd_resolution_signature",
                column: "signature_sha256",
                unique: true);

            // Append-only на уровне БД.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION vnd_resolution_signature_block_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'vnd_resolution_signature is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_vnd_resolution_signature_append_only
                BEFORE UPDATE OR DELETE ON vnd_resolution_signature
                FOR EACH ROW EXECUTE FUNCTION vnd_resolution_signature_block_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_vnd_resolution_signature_append_only ON vnd_resolution_signature;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS vnd_resolution_signature_block_changes();");
            migrationBuilder.DropTable(name: "vnd_resolution_signature");
        }
    }
}
