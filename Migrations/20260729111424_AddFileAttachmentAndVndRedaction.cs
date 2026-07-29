using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddFileAttachmentAndVndRedaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_vnd_id",
                table: "vnd_redaction");

            migrationBuilder.DeleteData(
                table: "vnd_redaction",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "vnd_redaction",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "vnd_redaction",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "vnd_redaction",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "vnd_redaction",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "attachment_ids",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_en",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_kg",
                table: "vnd_redaction");

            migrationBuilder.RenameColumn(
                name: "doc_ru",
                table: "vnd_redaction",
                newName: "code");

            migrationBuilder.AddColumn<int>(
                name: "approval_status",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "doc_file_en_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "doc_file_kg_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "doc_file_ru_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "number",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "requires_approval",
                table: "vnd_redaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "current_redaction_id",
                table: "vnd_document",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "file_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    bucket = table.Column<string>(type: "text", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vnd_redaction_attachment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false),
                    file_attachment_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_attachment_file_attachments_file_attachment_id",
                        column: x => x.file_attachment_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_attachment_vnd_redactions_vnd_redaction_id",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "current_redaction_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "current_redaction_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "current_redaction_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "current_redaction_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "current_redaction_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_code",
                table: "vnd_redaction",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_doc_file_en_id",
                table: "vnd_redaction",
                column: "doc_file_en_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_doc_file_kg_id",
                table: "vnd_redaction",
                column: "doc_file_kg_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_doc_file_ru_id",
                table: "vnd_redaction",
                column: "doc_file_ru_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_vnd_id_number",
                table: "vnd_redaction",
                columns: new[] { "vnd_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_current_redaction_id",
                table: "vnd_document",
                column: "current_redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_attachment_file_attachment_id",
                table: "vnd_redaction_attachment",
                column: "file_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_attachment_vnd_redaction_id_file_attachment_id",
                table: "vnd_redaction_attachment",
                columns: new[] { "vnd_redaction_id", "file_attachment_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_vnd_redactions_current_redaction_id",
                table: "vnd_document",
                column: "current_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_en_id",
                table: "vnd_redaction",
                column: "doc_file_en_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_kg_id",
                table: "vnd_redaction",
                column: "doc_file_kg_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_ru_id",
                table: "vnd_redaction",
                column: "doc_file_ru_id",
                principalTable: "file_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_vnd_redactions_current_redaction_id",
                table: "vnd_document");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_en_id",
                table: "vnd_redaction");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_kg_id",
                table: "vnd_redaction");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_file_attachments_doc_file_ru_id",
                table: "vnd_redaction");

            migrationBuilder.DropTable(
                name: "vnd_redaction_attachment");

            migrationBuilder.DropTable(
                name: "file_attachments");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_code",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_doc_file_en_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_doc_file_kg_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_doc_file_ru_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_vnd_id_number",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_document_current_redaction_id",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_file_en_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_file_kg_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "doc_file_ru_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "number",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "requires_approval",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "current_redaction_id",
                table: "vnd_document");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "vnd_redaction",
                newName: "doc_ru");

            migrationBuilder.AddColumn<int[]>(
                name: "attachment_ids",
                table: "vnd_redaction",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "doc_en",
                table: "vnd_redaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doc_kg",
                table: "vnd_redaction",
                type: "text",
                nullable: true);

            migrationBuilder.InsertData(
                table: "vnd_redaction",
                columns: new[] { "id", "attachment_ids", "created_at", "doc_en", "doc_kg", "doc_ru", "updated_at", "vnd_id" },
                values: new object[,]
                {
                    { 1, new int[0], new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision text in English...", "Редакциянын кыргыз тилиндеги тексти...", "Текст редакции на русском языке...", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, new int[0], new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision text in English...", "Редакциянын кыргыз тилиндеги тексти...", "Текст редакции на русском языке...", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2 },
                    { 3, new int[0], new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision text in English...", "Редакциянын кыргыз тилиндеги тексти...", "Текст редакции на русском языке...", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3 },
                    { 4, new int[0], new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision text in English...", "Редакциянын кыргыз тилиндеги тексти...", "Текст редакции на русском языке...", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4 },
                    { 5, new int[0], new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Revision text in English...", "Редакциянын кыргыз тилиндеги тексти...", "Текст редакции на русском языке...", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_vnd_id",
                table: "vnd_redaction",
                column: "vnd_id");
        }
    }
}
