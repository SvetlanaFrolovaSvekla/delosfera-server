using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Ссылки между ВНД с привязкой к редакции и месту в тексте:
    ///
    /// 1. vnd_link:
    ///    - kind - 0 = добавлена вручную, 1 = легаси-гиперссылка db://documents/{код} из текста
    ///      Word-файла редакции (isrib), проиндексированная автоматически (VndLegacyLinkIndexer);
    ///    - source_* - в какой редакции/на каком языке/в каком фрагменте текста ИСХОДНОГО документа
    ///      упоминается ссылка (всё null - "без упоминания в тексте", как было раньше);
    ///    - legacy_code - код документа из легаси-гиперссылки (для kind = 1);
    ///    - target_* - на какое место ЦЕЛЕВОГО документа ведёт ссылка (всё null - на весь документ);
    ///    - created_by_user_id / created_at.
    /// 2. vnd_redaction.legacy_links_scan_key - "отпечаток" файлов редакции, для которых
    ///    легаси-гиперссылки уже проиндексированы.
    ///
    /// Все новые колонки nullable (kind - с default 0) - существующие связи остаются как есть
    /// ("без упоминания в тексте", на весь документ).
    ///
    /// Миграция написана вручную (без Designer-файла) - как и 20260923110000_AddVndQuoteAnchorsAndRoundAttachments,
    /// атрибуты DbContext/Migration стоят прямо здесь; снапшот модели обновлён соответствующим образом.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924090000_AddVndLinkAnchors")]
    public partial class AddVndLinkAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "vnd_link",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "source_redaction_id",
                table: "vnd_link",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_target",
                table: "vnd_link",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_text",
                table: "vnd_link",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_prefix",
                table: "vnd_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_suffix",
                table: "vnd_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_occurrence",
                table: "vnd_link",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_code",
                table: "vnd_link",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_redaction_id",
                table: "vnd_link",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_document_target",
                table: "vnd_link",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_text",
                table: "vnd_link",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_prefix",
                table: "vnd_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_suffix",
                table: "vnd_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_occurrence",
                table: "vnd_link",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "created_by_user_id",
                table: "vnd_link",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "vnd_link",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_links_scan_key",
                table: "vnd_redaction",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_link_source_redaction_id",
                table: "vnd_link",
                column: "source_redaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_link_target_redaction_id",
                table: "vnd_link",
                column: "target_redaction_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redaction_source_redaction_id",
                table: "vnd_link",
                column: "source_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redaction_target_redaction_id",
                table: "vnd_link",
                column: "target_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redaction_source_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redaction_target_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropIndex(
                name: "ix_vnd_link_source_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropIndex(
                name: "ix_vnd_link_target_redaction_id",
                table: "vnd_link");

            // Автоматически проиндексированные легаси-ссылки без новых колонок теряют смысл -
            // до этой миграции они вычислялись на лету и в таблице не хранились.
            migrationBuilder.Sql("DELETE FROM vnd_link WHERE kind = 1;");

            foreach (var column in new[]
                     {
                         "kind", "source_redaction_id", "source_document_target", "source_text", "source_prefix",
                         "source_suffix", "source_occurrence", "legacy_code", "target_redaction_id",
                         "target_document_target", "target_text", "target_prefix", "target_suffix",
                         "target_occurrence", "created_by_user_id", "created_at",
                     })
            {
                migrationBuilder.DropColumn(name: column, table: "vnd_link");
            }

            migrationBuilder.DropColumn(
                name: "legacy_links_scan_key",
                table: "vnd_redaction");
        }
    }
}
