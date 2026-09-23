using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Несколько листов согласования у одной редакции (актуализация без изменений с
    /// согласованием - см. VndRedactionApprovalSheet) + признак такого процесса согласования
    /// (VndApprovalProcess.IsNoChangesActualization).
    ///
    /// Миграция написана вручную (без Designer-файла), поэтому атрибуты DbContext/Migration
    /// стоят прямо здесь. Имена ограничений сверены с тем, что генерирует сам EF (пробная
    /// миграция), снапшот модели тоже сгенерирован EF.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260923090000_AddVndRedactionApprovalSheets")]
    public partial class AddVndRedactionApprovalSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_no_changes_actualization",
                table: "vnd_approval_process",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "vnd_redaction_approval_sheet",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vnd_redaction_id = table.Column<int>(type: "integer", nullable: false),
                    approval_process_id = table.Column<int>(type: "integer", nullable: true),
                    file_attachment_id = table.Column<int>(type: "integer", nullable: false),
                    is_no_changes_actualization = table.Column<bool>(type: "boolean", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_redaction_approval_sheet", x => x.id);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_approval_sheet_file_attachments_file_attachme",
                        column: x => x.file_attachment_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_approval_sheet_vnd_approval_processes_approva",
                        column: x => x.approval_process_id,
                        principalTable: "vnd_approval_process",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vnd_redaction_approval_sheet_vnd_redactions_vnd_redaction_id",
                        column: x => x.vnd_redaction_id,
                        principalTable: "vnd_redaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_approval_sheet_approval_process_id",
                table: "vnd_redaction_approval_sheet",
                column: "approval_process_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_approval_sheet_file_attachment_id",
                table: "vnd_redaction_approval_sheet",
                column: "file_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_approval_sheet_vnd_redaction_id",
                table: "vnd_redaction_approval_sheet",
                column: "vnd_redaction_id");

            // --- Перенос существующих данных ---

            // 1. Процесс, запущенный по редакции, у которой УЖЕ был согласованный процесс раньше, -
            // это и есть актуализация без изменений: другого способа повторно отправить на
            // согласование не-черновик нет (см. VndApprovalService.StartAsync).
            migrationBuilder.Sql(@"
UPDATE vnd_approval_process p
SET is_no_changes_actualization = TRUE
WHERE EXISTS (
    SELECT 1 FROM vnd_approval_process q
    WHERE q.redaction_id = p.redaction_id
      AND q.id <> p.id
      AND q.status = 4
      AND q.created_at < p.created_at);");

            // 2. Листы, которые раньше затирались при повторном согласовании, физически остались в
            // file_attachments - находим сформированный системой лист для КАЖДОГО согласованного
            // процесса: то же имя файла, тот же автор (инициатор) и время создания рядом с
            // моментом завершения согласования.
            migrationBuilder.Sql(@"
INSERT INTO vnd_redaction_approval_sheet
    (vnd_redaction_id, approval_process_id, file_attachment_id, is_no_changes_actualization, approved_at, created_at)
SELECT r.id, p.id, f.id, p.is_no_changes_actualization, p.completed_at, now()
FROM vnd_approval_process p
JOIN vnd_redaction r ON r.id = p.redaction_id
JOIN LATERAL (
    SELECT fa.id
    FROM file_attachments fa
    WHERE fa.original_file_name = r.code || '_Лист_согласования.docx'
      AND fa.uploaded_by_user_id = p.initiator_user_id
      AND fa.created_at BETWEEN p.completed_at - INTERVAL '10 minutes'
                            AND p.completed_at + INTERVAL '10 minutes'
    ORDER BY ABS(EXTRACT(EPOCH FROM (fa.created_at - p.completed_at)))
    LIMIT 1
) f ON TRUE
WHERE p.status = 4 AND p.completed_at IS NOT NULL;");

            // 3. Текущий лист редакции, который не нашёлся ни за одним процессом (приложен
            // главным редактором вручную и т.п.) - тоже переносим, без привязки к процессу.
            migrationBuilder.Sql(@"
INSERT INTO vnd_redaction_approval_sheet
    (vnd_redaction_id, approval_process_id, file_attachment_id, is_no_changes_actualization, approved_at, created_at)
SELECT r.id, NULL, r.approval_sheet_file_id, FALSE, COALESCE(fa.created_at, now()), now()
FROM vnd_redaction r
LEFT JOIN file_attachments fa ON fa.id = r.approval_sheet_file_id
WHERE r.approval_sheet_file_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM vnd_redaction_approval_sheet s
      WHERE s.vnd_redaction_id = r.id AND s.file_attachment_id = r.approval_sheet_file_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_redaction_approval_sheet");

            migrationBuilder.DropColumn(
                name: "is_no_changes_actualization",
                table: "vnd_approval_process");
        }
    }
}
