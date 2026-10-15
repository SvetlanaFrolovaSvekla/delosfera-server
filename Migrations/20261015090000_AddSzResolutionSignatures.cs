using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Журнал подписей ЭЦП ГП «Инфоком» под резолюциями согласующих служебных записок — см.
    /// SzResolutionSignature. Миграция написана вручную (без Designer-файла, как и остальные последние
    /// миграции проекта), снапшот модели обновлён.
    ///
    /// Как и у журнала ВНД, создаётся триггер, запрещающий UPDATE/DELETE строк: журнал неизменяем даже
    /// для того, кто работает с БД в обход приложения.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261015090000_AddSzResolutionSignatures")]
    public partial class AddSzResolutionSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sz_resolution_signature",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    route_participant_id = table.Column<int>(type: "integer", nullable: false),
                    signature_id = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    content_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_sz_resolution_signature", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_resolution_signature_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_resolution_signature_route_participant_route_participant_id",
                        column: x => x.route_participant_id,
                        principalTable: "route_participant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_resolution_signature_signature_signature_id",
                        column: x => x.signature_id,
                        principalTable: "signature",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sz_resolution_signature_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_resolution_signature_person_idnp",
                table: "sz_resolution_signature",
                column: "person_idnp");

            migrationBuilder.CreateIndex(
                name: "ix_sz_resolution_signature_signature_id",
                table: "sz_resolution_signature",
                column: "signature_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_resolution_signature_sz_document_id",
                table: "sz_resolution_signature",
                column: "sz_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_resolution_signature_user_id",
                table: "sz_resolution_signature",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_sz_resolution_signature_route_participant_id",
                table: "sz_resolution_signature",
                column: "route_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sz_resolution_signature_signature_sha256",
                table: "sz_resolution_signature",
                column: "signature_sha256",
                unique: true);

            // Append-only на уровне БД.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sz_resolution_signature_block_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'sz_resolution_signature is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_sz_resolution_signature_append_only
                BEFORE UPDATE OR DELETE ON sz_resolution_signature
                FOR EACH ROW EXECUTE FUNCTION sz_resolution_signature_block_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_sz_resolution_signature_append_only ON sz_resolution_signature;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sz_resolution_signature_block_changes();");
            migrationBuilder.DropTable(name: "sz_resolution_signature");
        }
    }
}
