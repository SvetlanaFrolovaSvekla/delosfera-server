using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class _PendingProbe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redaction_source_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redaction_target_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_user_author_user_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_user_read_by_user_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_vnd_redaction_redaction_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_attachment_vnd_proposal_proposal_id",
                table: "vnd_proposal_attachment");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redactions_source_redaction_id",
                table: "vnd_link",
                column: "source_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redactions_target_redaction_id",
                table: "vnd_link",
                column: "target_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_users_author_user_id",
                table: "vnd_proposal",
                column: "author_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_users_read_by_user_id",
                table: "vnd_proposal",
                column: "read_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_vnd_redactions_redaction_id",
                table: "vnd_proposal",
                column: "redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_attachment_vnd_proposals_proposal_id",
                table: "vnd_proposal_attachment",
                column: "proposal_id",
                principalTable: "vnd_proposal",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redactions_source_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_link_vnd_redactions_target_redaction_id",
                table: "vnd_link");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_users_author_user_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_users_read_by_user_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_vnd_redactions_redaction_id",
                table: "vnd_proposal");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_proposal_attachment_vnd_proposals_proposal_id",
                table: "vnd_proposal_attachment");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redaction_source_redaction_id",
                table: "vnd_link",
                column: "source_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_link_vnd_redaction_target_redaction_id",
                table: "vnd_link",
                column: "target_redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_user_author_user_id",
                table: "vnd_proposal",
                column: "author_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_user_read_by_user_id",
                table: "vnd_proposal",
                column: "read_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_vnd_redaction_redaction_id",
                table: "vnd_proposal",
                column: "redaction_id",
                principalTable: "vnd_redaction",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_proposal_attachment_vnd_proposal_proposal_id",
                table: "vnd_proposal_attachment",
                column: "proposal_id",
                principalTable: "vnd_proposal",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
