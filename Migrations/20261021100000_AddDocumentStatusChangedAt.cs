using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Момент входа документа в текущий статус (document.status_changed_at) — для доски
    /// служебных записок: «зависла» считается по времени на текущей стадии, а не от даты создания.
    ///
    /// У существующих документов время восстанавливается из аудита: каждая смена статуса идёт через
    /// DocumentService.ChangeStatusAsync и пишет запись Document/StatusChanged. Документ, статус
    /// которого ни разу не менялся, в своём статусе с момента создания. Дальше значение проставляет
    /// DelosferaDbContext.ApplyAuditInfo. Миграция написана вручную (без Designer-файла), снапшот
    /// модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261021100000_AddDocumentStatusChangedAt")]
    public partial class AddDocumentStatusChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at",
                table: "document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE document d
                SET status_changed_at = COALESCE(
                    (SELECT max(a.at)
                     FROM audit_entry a
                     WHERE a.entity_type = 'Document'
                       AND a.entity_id = d.id
                       AND a.action = 'StatusChanged'),
                    d.created_at);
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "status_changed_at",
                table: "document",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status_changed_at",
                table: "document");
        }
    }
}
