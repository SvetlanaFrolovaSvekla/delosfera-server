using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddRedactionDocumentUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "doc_en_updated_at",
                table: "vnd_redaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "doc_kg_updated_at",
                table: "vnd_redaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "doc_ru_updated_at",
                table: "vnd_redaction",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "doc_en_updated_at",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_kg_updated_at",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_ru_updated_at",
                table: "vnd_redaction");
        }
    }
}
