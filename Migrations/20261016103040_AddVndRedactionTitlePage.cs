using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndRedactionTitlePage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "title_page_file_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "title_page_is_manual",
                table: "vnd_redaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "title_page_updated_at",
                table: "vnd_redaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vnd_redaction_decision",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decision_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actor_user_id = table.Column<int>(type: "integer", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    actor_position = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_decision", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_decision_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_decision_vnd_redactions_vnd_redaction_id",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_title_page_file_id",
                table: "vnd_redaction",
                column: "title_page_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_decision_actor_user_id",
                table: "vnd_redaction_decision",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_decision_vnd_redaction_id_created_at",
                table: "vnd_redaction_decision",
                columns: new[] { "vnd_redaction_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_file_attachments_title_page_file_id",
                table: "vnd_redaction",
                column: "title_page_file_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_file_attachments_title_page_file_id",
                table: "vnd_redaction");

            migrationBuilder.DropTable(
                name: "vnd_redaction_decision");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_title_page_file_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_page_file_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_page_is_manual",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_page_updated_at",
                table: "vnd_redaction");
        }
    }
}
