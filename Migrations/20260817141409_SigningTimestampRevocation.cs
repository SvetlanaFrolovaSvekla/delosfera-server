using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SigningTimestampRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "revocation_checked_at",
                table: "user_certificate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "timestamp_authority",
                table: "signature",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "timestamp_token",
                table: "signature",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "timestamped_at",
                table: "signature",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "signing_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timestamp_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    timestamp_authority_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    timestamp_required = table.Column<bool>(type: "boolean", nullable: false),
                    timestamp_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    revocation_check_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    revocation_strict = table.Column<bool>(type: "boolean", nullable: false),
                    revocation_recheck_hours = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signing_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "signing_settings");

            migrationBuilder.DropColumn(
                name: "revocation_checked_at",
                table: "user_certificate");

            migrationBuilder.DropColumn(
                name: "timestamp_authority",
                table: "signature");

            migrationBuilder.DropColumn(
                name: "timestamp_token",
                table: "signature");

            migrationBuilder.DropColumn(
                name: "timestamped_at",
                table: "signature");
        }
    }
}
