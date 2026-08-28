using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Перезапись role.permission_codes из этой миграции убрана намеренно.
    ///
    /// EF вписал ролям «Администратор» и «Главный редактор ВНД» снимок набора
    /// прав «1..51» — таким он получился в коде после появления прав закупок.
    /// Снимок затирает то, что банк настроил руками: если у роли осознанно
    /// забрали право, миграция вернула бы его молча.
    ///
    /// Права на новые разделы раздаёт RolePermissionDefaults при старте: он
    /// добавляет недостающее и ничего не отбирает. Это уже второй раз, когда
    /// снимок прав в миграции пришлось убирать, — см. миграцию
    /// MakeApprovalProcessRedactionIndexPartial.
    /// </remarks>
    public partial class ProcurementPlanLinkAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "plan_item_id",
                table: "procurement_request",
                type: "integer",
                nullable: true);



            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_plan_item_id",
                table: "procurement_request",
                column: "plan_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_procurement_request_procurement_plan_item_plan_item_id",
                table: "procurement_request",
                column: "plan_item_id",
                principalTable: "procurement_plan_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_procurement_request_procurement_plan_item_plan_item_id",
                table: "procurement_request");

            migrationBuilder.DropIndex(
                name: "ix_procurement_request_plan_item_id",
                table: "procurement_request");

            migrationBuilder.DropColumn(
                name: "plan_item_id",
                table: "procurement_request");


        }
    }
}
