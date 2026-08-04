using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndActualizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "actualization_requires_approval",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "actualization_responsible_user_id",
                table: "vnd_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "actualization_shift_next_period",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "period",
                table: "vnd_document",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "vnd_actualization_request",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decided_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_actualization_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_request_user_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_request_user_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_actualization_request_vnd_documents_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "actualization_requires_approval", "actualization_responsible_user_id", "actualization_shift_next_period", "period" },
                values: new object[] { false, null, false, 2 });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_actualization_responsible_user_id",
                table: "vnd_document",
                column: "actualization_responsible_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_request_decided_by_user_id",
                table: "vnd_actualization_request",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_request_requested_by_user_id",
                table: "vnd_actualization_request",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_actualization_request_vnd_id_requested_by_user_id_status",
                table: "vnd_actualization_request",
                columns: new[] { "vnd_id", "requested_by_user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_user_actualization_responsible_user_id",
                table: "vnd_document",
                column: "actualization_responsible_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_user_actualization_responsible_user_id",
                table: "vnd_document");

            migrationBuilder.DropTable(
                name: "vnd_actualization_request");

            migrationBuilder.DropIndex(
                name: "ix_vnd_document_actualization_responsible_user_id",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "actualization_requires_approval",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "actualization_responsible_user_id",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "actualization_shift_next_period",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "period",
                table: "vnd_document");
        }
    }
}
