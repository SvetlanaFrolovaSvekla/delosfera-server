using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ContractRulesAndProtocolPerMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_procurement_protocol_request_id",
                table: "procurement_protocol");

            migrationBuilder.AddColumn<bool>(
                name: "is_non_resident",
                table: "procurement_supplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "meeting_date",
                table: "procurement_protocol",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_request_id_meeting_date",
                table: "procurement_protocol",
                columns: new[] { "request_id", "meeting_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_procurement_protocol_request_id_meeting_date",
                table: "procurement_protocol");

            migrationBuilder.DropColumn(
                name: "is_non_resident",
                table: "procurement_supplier");

            migrationBuilder.DropColumn(
                name: "meeting_date",
                table: "procurement_protocol");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_request_id",
                table: "procurement_protocol",
                column: "request_id",
                unique: true);
        }
    }
}
