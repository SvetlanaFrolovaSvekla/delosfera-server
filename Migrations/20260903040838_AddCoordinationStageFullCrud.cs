using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinationStageFullCrud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vnd_coordination_default_approver_kind",
                table: "vnd_coordination_default_approver");

            migrationBuilder.RenameColumn(
                name: "kind",
                table: "vnd_coordination_default_approver",
                newName: "org_unit_id");

            migrationBuilder.AddColumn<int>(
                name: "order",
                table: "vnd_coordination_default_approver",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "vnd_coordination_default_approver",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "coordination_stage_id",
                table: "vnd_approval_stage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "vnd_approval_stage",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "order", "org_unit_id", "title" },
                values: new object[] { 1, 34, "Юридическое управление" });

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "order", "org_unit_id", "title" },
                values: new object[] { 2, 28, "Риск-менеджмент" });

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "order", "org_unit_id", "title" },
                values: new object[] { 3, 5, "Комплаенс-контроль" });

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "order", "org_unit_id", "title" },
                values: new object[] { 4, 52, "Методология" });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_coordination_default_approver_order",
                table: "vnd_coordination_default_approver",
                column: "order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_coordination_default_approver_org_unit_id",
                table: "vnd_coordination_default_approver",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_approval_stage_coordination_stage_id",
                table: "vnd_approval_stage",
                column: "coordination_stage_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_approval_stage_vnd_coordination_default_approver_coordi",
                table: "vnd_approval_stage",
                column: "coordination_stage_id",
                principalTable: "vnd_coordination_default_approver",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_coordination_default_approver_dictionary_organization_u",
                table: "vnd_coordination_default_approver",
                column: "org_unit_id",
                principalTable: "dictionary_organization_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_approval_stage_vnd_coordination_default_approver_coordi",
                table: "vnd_approval_stage");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_coordination_default_approver_dictionary_organization_u",
                table: "vnd_coordination_default_approver");

            migrationBuilder.DropIndex(
                name: "ix_vnd_coordination_default_approver_order",
                table: "vnd_coordination_default_approver");

            migrationBuilder.DropIndex(
                name: "ix_vnd_coordination_default_approver_org_unit_id",
                table: "vnd_coordination_default_approver");

            migrationBuilder.DropIndex(
                name: "ix_vnd_approval_stage_coordination_stage_id",
                table: "vnd_approval_stage");

            migrationBuilder.DropColumn(
                name: "order",
                table: "vnd_coordination_default_approver");

            migrationBuilder.DropColumn(
                name: "title",
                table: "vnd_coordination_default_approver");

            migrationBuilder.DropColumn(
                name: "coordination_stage_id",
                table: "vnd_approval_stage");

            migrationBuilder.DropColumn(
                name: "title",
                table: "vnd_approval_stage");

            migrationBuilder.RenameColumn(
                name: "org_unit_id",
                table: "vnd_coordination_default_approver",
                newName: "kind");

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                column: "kind",
                value: 0);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 2,
                column: "kind",
                value: 1);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 3,
                column: "kind",
                value: 2);

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                column: "kind",
                value: 4);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_coordination_default_approver_kind",
                table: "vnd_coordination_default_approver",
                column: "kind",
                unique: true);
        }
    }
}
