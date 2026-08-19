using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_until",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "locked_until",
                value: null);

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "locked_until",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "user");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "user");
        }
    }
}
