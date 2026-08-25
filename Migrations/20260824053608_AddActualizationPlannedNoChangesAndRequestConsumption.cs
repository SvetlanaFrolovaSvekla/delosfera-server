using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddActualizationPlannedNoChangesAndRequestConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "actualization_planned_no_changes",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "consumed_at",
                table: "vnd_actualization_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "planned_no_changes",
                table: "vnd_actualization_request",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "planned_no_changes",
                table: "vnd_actualization_record",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "actualization_planned_no_changes",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "actualization_planned_no_changes",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "actualization_planned_no_changes",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "actualization_planned_no_changes",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "actualization_planned_no_changes",
                value: false);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 6,
                column: "actualization_planned_no_changes",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actualization_planned_no_changes",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "consumed_at",
                table: "vnd_actualization_request");

            migrationBuilder.DropColumn(
                name: "planned_no_changes",
                table: "vnd_actualization_request");

            migrationBuilder.DropColumn(
                name: "planned_no_changes",
                table: "vnd_actualization_record");
        }
    }
}
