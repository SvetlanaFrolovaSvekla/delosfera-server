using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>vnd_redaction.legacy_attachment_refs - номера вложений из легаси-гиперссылок
    /// db://attachments/{n} в тексте редакции по языкам ("ru:1,2;kg:1"). Заполняется
    /// VndLegacyLinkIndexer (версия отпечатка поднята до v2 - все редакции переиндексируются
    /// автоматически). Нужна, чтобы на вкладке «Связанные документы» показывать, в тексте какой
    /// редакции найдена ссылка на вложение, без скачивания Word-файлов на каждый запрос.
    ///
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924120000_AddVndRedactionLegacyAttachmentRefs")]
    public partial class AddVndRedactionLegacyAttachmentRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "legacy_attachment_refs",
                table: "vnd_redaction",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "legacy_attachment_refs",
                table: "vnd_redaction");
        }
    }
}
