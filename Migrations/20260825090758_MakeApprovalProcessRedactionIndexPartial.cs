using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class MakeApprovalProcessRedactionIndexPartial : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Перезапись role.permission_codes из этой миграции убрана намеренно.
        ///
        /// Она вписывала ролям «Администратор» и «Главный редактор ВНД» набор
        /// прав до 37 включительно — таким он был в ветке разработки. На стенде
        /// у этих ролей права до 43: доверенности, корреспонденция, банковская
        /// тайна, кадровые приказы. Применив её как есть, мы молча срезали бы
        /// шесть прав у одиннадцати человек, и заметили бы это тогда, когда
        /// администратор не смог бы открыть доверенности.
        ///
        /// Списком прав распоряжается справочник ролей, а не миграция: набор
        /// меняется по мере появления возможностей, и фиксировать его снимком
        /// на дату — значит однажды откатить чужую работу.
        ///
        /// Частичный индекс — то, ради чего миграция и заводилась, — сохранён.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vnd_approval_process_redaction_id",
                table: "vnd_approval_process");



            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_process_redaction_id",
                table: "vnd_approval_process",
                column: "redaction_id",
                unique: true,
                filter: "status NOT IN (4, 5, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vnd_approval_process_redaction_id",
                table: "vnd_approval_process");



            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_process_redaction_id",
                table: "vnd_approval_process",
                column: "redaction_id",
                unique: true);
        }
    }
}
