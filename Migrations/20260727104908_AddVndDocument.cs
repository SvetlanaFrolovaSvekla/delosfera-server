using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_document",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    developer_id = table.Column<int>(type: "integer", nullable: false),
                    curator_developer_id = table.Column<int>(type: "integer", nullable: true),
                    organ_id = table.Column<int>(type: "integer", nullable: false),
                    adoption_date = table.Column<DateOnly>(type: "date", nullable: true),
                    adoption_code = table.Column<string>(type: "text", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requisites_changed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    revision_changed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cancel_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cancel_code = table.Column<string>(type: "text", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    archived_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_actualization_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_actualization_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_actualization_had_changes = table.Column<bool>(type: "boolean", nullable: false),
                    rubric_id = table.Column<int>(type: "integer", nullable: false),
                    secrecy_level_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_document_dictionary_approval_body_organ_id",
                        column: x => x.organ_id,
                        principalTable: "dictionary_approval_body",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_document_dictionary_organization_unit_developer_id",
                        column: x => x.developer_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_document_dictionary_rubric_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "dictionary_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_document_dictionary_security_level_secrecy_level_id",
                        column: x => x.secrecy_level_id,
                        principalTable: "dictionary_security_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_document_dictionary_type_vnd_type_id",
                        column: x => x.type_id,
                        principalTable: "dictionary_type_vnd",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_document_user_curator_developer_id",
                        column: x => x.curator_developer_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vnd_keyword",
                columns: table => new
                {
                    keyword_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_keyword", x => new { x.keyword_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_keyword_dictionary_keyword_keyword_id",
                        column: x => x.keyword_id,
                        principalTable: "dictionary_keyword",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_keyword_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_link",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_vnd_id = table.Column<int>(type: "integer", nullable: false),
                    target_vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_link_vnd_document_source_vnd_id",
                        column: x => x.source_vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_link_vnd_document_target_vnd_id",
                        column: x => x.target_vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vnd_redaction",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_id = table.Column<int>(type: "integer", nullable: false),
                    doc_ru = table.Column<string>(type: "text", nullable: false),
                    doc_kg = table.Column<string>(type: "text", nullable: true),
                    doc_en = table.Column<string>(type: "text", nullable: true),
                    attachment_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_responsible_executor",
                columns: table => new
                {
                    organization_unit_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_responsible_executor", x => new { x.organization_unit_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_responsible_executor_dictionary_organization_unit_organ",
                        column: x => x.organization_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_responsible_executor_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vnd_user_group",
                columns: table => new
                {
                    user_group_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_user_group", x => new { x.user_group_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_user_group_dictionary_user_group_user_group_id",
                        column: x => x.user_group_id,
                        principalTable: "dictionary_user_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_user_group_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "vnd_document",
                columns: new[] { "id", "adoption_code", "adoption_date", "archived_date", "cancel_code", "cancel_date", "cancel_reason", "code", "created_at", "curator_developer_id", "developer_id", "due_actualization_date", "effective_date", "last_actualization_date", "last_actualization_had_changes", "name", "organ_id", "requisites_changed_date", "revision_changed_date", "rubric_id", "secrecy_level_id", "status", "type_id", "updated_at" },
                values: new object[,]
                {
                    { 1, "пр. №4(2)", new DateOnly(2023, 2, 9), null, null, null, null, "10062", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 26, new DateOnly(2026, 8, 9), new DateOnly(2023, 2, 16), new DateOnly(2025, 8, 9), true, "Порядок работы с обеспечением (залогами)", 3, new DateOnly(2026, 1, 12), new DateOnly(2026, 7, 20), 5, 3, 3, 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "пр. №9(1)", new DateOnly(2021, 3, 14), null, null, null, null, "10084", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 26, new DateOnly(2026, 7, 22), new DateOnly(2021, 4, 1), new DateOnly(2025, 7, 22), false, "Политика управления кредитными рисками", 7, new DateOnly(2025, 5, 5), new DateOnly(2026, 6, 18), 5, 2, 0, 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "пр. №2(5)", new DateOnly(2020, 1, 20), null, null, null, null, "10011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 11, 38, new DateOnly(2026, 7, 25), new DateOnly(2020, 2, 1), new DateOnly(2025, 7, 25), true, "Регламент кассовых операций", 3, new DateOnly(2024, 10, 10), new DateOnly(2026, 7, 1), 15, 1, 1, 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "пр. ОСА-1", new DateOnly(2019, 5, 5), null, null, null, null, "10201", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 9, 32, new DateOnly(2026, 9, 28), new DateOnly(2019, 6, 1), new DateOnly(2025, 9, 28), false, "Кодекс корпоративной этики", 2, new DateOnly(2024, 3, 1), new DateOnly(2025, 9, 14), 7, 1, 0, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "пр. №1(4)", new DateOnly(2019, 1, 10), new DateOnly(2024, 3, 20), "пр. №8(3)", new DateOnly(2024, 3, 14), "Заменён новой редакцией ВНД-037 v4.1", "10037", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 12, 4, null, new DateOnly(2019, 2, 1), null, false, "Регламент управления ликвидностью (ред. 2019)", 3, new DateOnly(2019, 2, 1), new DateOnly(2019, 2, 1), 11, 2, 4, 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "vnd_keyword",
                columns: new[] { "keyword_id", "vnd_id" },
                values: new object[,]
                {
                    { 6, 1 },
                    { 6, 4 },
                    { 8, 3 },
                    { 11, 2 },
                    { 12, 1 }
                });

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

            migrationBuilder.InsertData(
                table: "vnd_responsible_executor",
                columns: new[] { "organization_unit_id", "vnd_id" },
                values: new object[,]
                {
                    { 4, 5 },
                    { 8, 2 },
                    { 26, 1 },
                    { 26, 2 },
                    { 32, 4 },
                    { 38, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_code",
                table: "vnd_document",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_curator_developer_id",
                table: "vnd_document",
                column: "curator_developer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_developer_id",
                table: "vnd_document",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_organ_id",
                table: "vnd_document",
                column: "organ_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_rubric_id",
                table: "vnd_document",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_secrecy_level_id",
                table: "vnd_document",
                column: "secrecy_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_type_id",
                table: "vnd_document",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_keyword_vnd_id",
                table: "vnd_keyword",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_link_source_vnd_id",
                table: "vnd_link",
                column: "source_vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_link_target_vnd_id",
                table: "vnd_link",
                column: "target_vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_vnd_id",
                table: "vnd_redaction",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_responsible_executor_vnd_id",
                table: "vnd_responsible_executor",
                column: "vnd_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_user_group_vnd_id",
                table: "vnd_user_group",
                column: "vnd_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_keyword");

            migrationBuilder.DropTable(
                name: "vnd_link");

            migrationBuilder.DropTable(
                name: "vnd_redaction");

            migrationBuilder.DropTable(
                name: "vnd_responsible_executor");

            migrationBuilder.DropTable(
                name: "vnd_user_group");

            migrationBuilder.DropTable(
                name: "vnd_document");
        }
    }
}
