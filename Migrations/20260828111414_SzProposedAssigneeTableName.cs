using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzProposedAssigneeTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sz_proposed_assignees_sz_documents_sz_document_id",
                table: "sz_proposed_assignees");

            migrationBuilder.DropForeignKey(
                name: "fk_sz_proposed_assignees_users_user_id",
                table: "sz_proposed_assignees");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sz_proposed_assignees",
                table: "sz_proposed_assignees");

            migrationBuilder.DropIndex(
                name: "ix_sz_proposed_assignees_sz_document_id",
                table: "sz_proposed_assignees");

            migrationBuilder.RenameTable(
                name: "sz_proposed_assignees",
                newName: "sz_proposed_assignee");

            migrationBuilder.RenameIndex(
                name: "ix_sz_proposed_assignees_user_id",
                table: "sz_proposed_assignee",
                newName: "ix_sz_proposed_assignee_user_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sz_proposed_assignee",
                table: "sz_proposed_assignee",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_proposed_assignee_sz_document_id_user_id",
                table: "sz_proposed_assignee",
                columns: new[] { "sz_document_id", "user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_sz_proposed_assignee_sz_document_sz_document_id",
                table: "sz_proposed_assignee",
                column: "sz_document_id",
                principalTable: "sz_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sz_proposed_assignee_users_user_id",
                table: "sz_proposed_assignee",
                column: "user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sz_proposed_assignee_sz_document_sz_document_id",
                table: "sz_proposed_assignee");

            migrationBuilder.DropForeignKey(
                name: "fk_sz_proposed_assignee_users_user_id",
                table: "sz_proposed_assignee");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sz_proposed_assignee",
                table: "sz_proposed_assignee");

            migrationBuilder.DropIndex(
                name: "ix_sz_proposed_assignee_sz_document_id_user_id",
                table: "sz_proposed_assignee");

            migrationBuilder.RenameTable(
                name: "sz_proposed_assignee",
                newName: "sz_proposed_assignees");

            migrationBuilder.RenameIndex(
                name: "ix_sz_proposed_assignee_user_id",
                table: "sz_proposed_assignees",
                newName: "ix_sz_proposed_assignees_user_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sz_proposed_assignees",
                table: "sz_proposed_assignees",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_proposed_assignees_sz_document_id",
                table: "sz_proposed_assignees",
                column: "sz_document_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sz_proposed_assignees_sz_documents_sz_document_id",
                table: "sz_proposed_assignees",
                column: "sz_document_id",
                principalTable: "sz_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sz_proposed_assignees_users_user_id",
                table: "sz_proposed_assignees",
                column: "user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
