using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndRedactionRevisionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Таблицу substitution_approvals и её индексы (ix_substitution_approvals_*) сюда не
            // включаем: авто-генерация снова подхватила их как "дрейф" из-за того же
            // рассинхрона снапшота, что уже был у миграции AddSubstitutionApprovals (см. её
            // комментарий) - на самом деле их создаёт именно она, применяется раньше этой по
            // порядку. Повторное создание здесь падало бы с "relation already exists".
            migrationBuilder.CreateTable(
                name: "vnd_redaction_revision_snapshot",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false),
                    approval_process_id = table.Column<int>(type: "integer", nullable: false),
                    snapshot_number = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: true),
                    doc_file_ru_id = table.Column<int>(type: "integer", nullable: false),
                    doc_file_kg_id = table.Column<int>(type: "integer", nullable: true),
                    doc_file_en_id = table.Column<int>(type: "integer", nullable: true),
                    tid_file_id = table.Column<int>(type: "integer", nullable: true),
                    disagreement_matrix_file_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_revision_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_file_attachments_disagreeme",
                        column: x => x.disagreement_matrix_file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_file_attachments_doc_file_e",
                        column: x => x.doc_file_en_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_file_attachments_doc_file_k",
                        column: x => x.doc_file_kg_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_file_attachments_doc_file_r",
                        column: x => x.doc_file_ru_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_file_attachments_tid_file_id",
                        column: x => x.tid_file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_vnd_approval_process_approv",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_revision_snapshot_vnd_redaction_vnd_redaction",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_approval_process_id",
                table: "vnd_redaction_revision_snapshot",
                column: "approval_process_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_disagreement_matrix_file_id",
                table: "vnd_redaction_revision_snapshot",
                column: "disagreement_matrix_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_doc_file_en_id",
                table: "vnd_redaction_revision_snapshot",
                column: "doc_file_en_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_doc_file_kg_id",
                table: "vnd_redaction_revision_snapshot",
                column: "doc_file_kg_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_doc_file_ru_id",
                table: "vnd_redaction_revision_snapshot",
                column: "doc_file_ru_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_tid_file_id",
                table: "vnd_redaction_revision_snapshot",
                column: "tid_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_revision_snapshot_vnd_redaction_id_snapshot_n",
                table: "vnd_redaction_revision_snapshot",
                columns: new[] { "vnd_redaction_id", "snapshot_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_redaction_revision_snapshot");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 });
        }
    }
}