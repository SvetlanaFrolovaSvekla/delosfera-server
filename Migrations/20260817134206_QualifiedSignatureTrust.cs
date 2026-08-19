using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class QualifiedSignatureTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trusted_certificate_authority",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    issuer = table.Column<string>(type: "text", nullable: false),
                    thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    raw_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    added_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disabled_reason = table.Column<string>(type: "text", nullable: true),
                    disabled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trusted_certificate_authority", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_certificate",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    issuer = table.Column<string>(type: "text", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    raw_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_certificate", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_certificate_authority_thumbprint",
                table: "trusted_certificate_authority",
                column: "thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_certificate_thumbprint",
                table: "user_certificate",
                column: "thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_certificate_user_id",
                table: "user_certificate",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trusted_certificate_authority");

            migrationBuilder.DropTable(
                name: "user_certificate");
        }
    }
}
