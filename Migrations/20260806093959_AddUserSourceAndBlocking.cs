using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSourceAndBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "block_reason",
                table: "user",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "blocked_at",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "blocked_by_user_id",
                table: "user",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "user",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 9,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "block_reason", "blocked_at", "blocked_by_user_id", "source" },
                values: new object[] { null, null, null, "Local" });

            migrationBuilder.CreateIndex(
                name: "ix_user_blocked_by_user_id",
                table: "user",
                column: "blocked_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_user_blocked_by_user_id",
                table: "user",
                column: "blocked_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_user_blocked_by_user_id",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_user_blocked_by_user_id",
                table: "user");

            migrationBuilder.DropColumn(
                name: "block_reason",
                table: "user");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                table: "user");

            migrationBuilder.DropColumn(
                name: "blocked_by_user_id",
                table: "user");

            migrationBuilder.DropColumn(
                name: "source",
                table: "user");
        }
    }
}
