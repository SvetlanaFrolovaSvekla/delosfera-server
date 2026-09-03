using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementSpecificationFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "specification_attachment_id",
                table: "procurement_request",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_specification_attachment_id",
                table: "procurement_request",
                column: "specification_attachment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_procurement_request_document_attachment_specification_attac",
                table: "procurement_request",
                column: "specification_attachment_id",
                principalTable: "document_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_procurement_request_document_attachment_specification_attac",
                table: "procurement_request");

            migrationBuilder.DropIndex(
                name: "ix_procurement_request_specification_attachment_id",
                table: "procurement_request");

            migrationBuilder.DropColumn(
                name: "specification_attachment_id",
                table: "procurement_request");
        }
    }
}
