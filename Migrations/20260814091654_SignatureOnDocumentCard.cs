using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SignatureOnDocumentCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "document_attachment_id",
                table: "signature",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "signature",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "document_id",
                table: "signature",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_signature_document_id",
                table: "signature",
                column: "document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_signature_document_id",
                table: "signature");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "signature");

            migrationBuilder.DropColumn(
                name: "document_id",
                table: "signature");

            migrationBuilder.AlterColumn<int>(
                name: "document_attachment_id",
                table: "signature",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
