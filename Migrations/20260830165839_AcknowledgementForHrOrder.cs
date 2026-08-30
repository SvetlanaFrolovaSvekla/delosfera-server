using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AcknowledgementForHrOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_acknowledgement_sheets_documents_document_id",
                table: "acknowledgement_sheets");

            migrationBuilder.AlterColumn<int>(
                name: "document_id",
                table: "acknowledgement_sheets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "hr_order_id",
                table: "acknowledgement_sheets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "hr_order_id1",
                table: "acknowledgement_sheets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_acknowledgement_sheets_hr_order_id1",
                table: "acknowledgement_sheets",
                column: "hr_order_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_acknowledgement_sheets_documents_document_id",
                table: "acknowledgement_sheets",
                column: "document_id",
                principalTable: "document",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_acknowledgement_sheets_hr_orders_hr_order_id1",
                table: "acknowledgement_sheets",
                column: "hr_order_id1",
                principalTable: "hr_order",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_acknowledgement_sheets_documents_document_id",
                table: "acknowledgement_sheets");

            migrationBuilder.DropForeignKey(
                name: "fk_acknowledgement_sheets_hr_orders_hr_order_id1",
                table: "acknowledgement_sheets");

            migrationBuilder.DropIndex(
                name: "ix_acknowledgement_sheets_hr_order_id1",
                table: "acknowledgement_sheets");

            migrationBuilder.DropColumn(
                name: "hr_order_id",
                table: "acknowledgement_sheets");

            migrationBuilder.DropColumn(
                name: "hr_order_id1",
                table: "acknowledgement_sheets");

            migrationBuilder.AlterColumn<int>(
                name: "document_id",
                table: "acknowledgement_sheets",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_acknowledgement_sheets_documents_document_id",
                table: "acknowledgement_sheets",
                column: "document_id",
                principalTable: "document",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
