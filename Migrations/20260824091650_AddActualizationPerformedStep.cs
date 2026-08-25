using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddActualizationPerformedStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "planned_no_changes",
                table: "vnd_actualization_request",
                newName: "shift_next_period");

            migrationBuilder.AddColumn<bool>(
                name: "actualization_performed",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "performed_at",
                table: "vnd_actualization_record",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "actualization_performed",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "actualization_performed",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "actualization_performed",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "actualization_performed",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "actualization_performed",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "actualization_performed",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actualization_performed",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "performed_at",
                table: "vnd_actualization_record");

            migrationBuilder.RenameColumn(
                name: "shift_next_period",
                table: "vnd_actualization_request",
                newName: "planned_no_changes");
        }
    }
}
