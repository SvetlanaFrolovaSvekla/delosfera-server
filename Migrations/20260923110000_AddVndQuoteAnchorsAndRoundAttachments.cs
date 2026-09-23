using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Замечания согласующих, привязанные к тексту редакции, и сохранение истории кругов:
    ///
    /// 1. vnd_approval_stage_quote: "якорь" цитаты (prefix/suffix/occurrence) - по нему клиент
    ///    находит ИМЕННО то место в документе, на которое сослался согласующий, даже если такая же
    ///    фраза встречается в тексте несколько раз; note - замечание к конкретному фрагменту.
    /// 2. vnd_approval_stage_attachment.phase_round_id - вложения к решениям завершённых кругов
    ///    повторного согласования/финальной выдержки больше не удаляются, а переезжают в архив
    ///    своего круга (см. VndApprovalService.ArchivePreviousRoundAttachmentsAsync).
    ///
    /// Все новые колонки nullable - существующие данные не меняются.
    ///
    /// Миграция написана вручную (без Designer-файла) - как и 20260923090000_AddVndRedactionApprovalSheets,
    /// атрибуты DbContext/Migration стоят прямо здесь; снапшот модели обновлён соответствующим образом.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260923110000_AddVndQuoteAnchorsAndRoundAttachments")]
    public partial class AddVndQuoteAnchorsAndRoundAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "prefix",
                table: "vnd_approval_stage_quote",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suffix",
                table: "vnd_approval_stage_quote",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "occurrence",
                table: "vnd_approval_stage_quote",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "vnd_approval_stage_quote",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "phase_round_id",
                table: "vnd_approval_stage_attachment",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_attachment_phase_round_id",
                table: "vnd_approval_stage_attachment",
                column: "phase_round_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_approval_stage_attachment_vnd_approval_phase_round_pha",
                table: "vnd_approval_stage_attachment",
                column: "phase_round_id",
                principalTable: "vnd_approval_phase_round",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_approval_stage_attachment_vnd_approval_phase_round_pha",
                table: "vnd_approval_stage_attachment");

            migrationBuilder.DropIndex(
                name: "ix_vnd_approval_stage_attachment_phase_round_id",
                table: "vnd_approval_stage_attachment");

            migrationBuilder.DropColumn(
                name: "phase_round_id",
                table: "vnd_approval_stage_attachment");

            migrationBuilder.DropColumn(
                name: "note",
                table: "vnd_approval_stage_quote");

            migrationBuilder.DropColumn(
                name: "occurrence",
                table: "vnd_approval_stage_quote");

            migrationBuilder.DropColumn(
                name: "suffix",
                table: "vnd_approval_stage_quote");

            migrationBuilder.DropColumn(
                name: "prefix",
                table: "vnd_approval_stage_quote");
        }
    }
}
