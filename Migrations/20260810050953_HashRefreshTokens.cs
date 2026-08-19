using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class HashRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "access_token",
                table: "token");

            migrationBuilder.RenameColumn(
                name: "refresh_token",
                table: "token",
                newName: "refresh_token_hash");

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "token",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "ix_token_refresh_token_hash",
                table: "token",
                column: "refresh_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_token_refresh_token_hash",
                table: "token");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "token");

            migrationBuilder.RenameColumn(
                name: "refresh_token_hash",
                table: "token",
                newName: "refresh_token");

            migrationBuilder.AddColumn<string>(
                name: "access_token",
                table: "token",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
