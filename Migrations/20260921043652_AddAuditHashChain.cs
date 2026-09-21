using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditHashChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hash",
                table: "audit_entry",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prev_hash",
                table: "audit_entry",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_hash",
                table: "audit_entry",
                column: "hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_entry_hash",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "hash",
                table: "audit_entry");

            migrationBuilder.DropColumn(
                name: "prev_hash",
                table: "audit_entry");
        }
    }
}
