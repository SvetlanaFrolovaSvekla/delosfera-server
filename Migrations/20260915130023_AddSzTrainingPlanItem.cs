using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddSzTrainingPlanItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "plan_item_id",
                table: "sz_document",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_plan_item_id",
                table: "sz_document",
                column: "plan_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sz_document_procurement_plan_item_plan_item_id",
                table: "sz_document",
                column: "plan_item_id",
                principalTable: "procurement_plan_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sz_document_procurement_plan_item_plan_item_id",
                table: "sz_document");

            migrationBuilder.DropIndex(
                name: "ix_sz_document_plan_item_id",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "plan_item_id",
                table: "sz_document");
        }
    }
}
