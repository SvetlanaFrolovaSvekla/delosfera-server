using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SyncVndPermissionsAndModuleMove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_record_user_responsible_user_id",
                table: "vnd_actualization_record");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_request_user_decided_by_user_id",
                table: "vnd_actualization_request");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_request_user_requested_by_user_id",
                table: "vnd_actualization_request");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_approval_stage_user_approver_user_id",
                table: "vnd_approval_stage");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_user_actualization_responsible_user_id",
                table: "vnd_document");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_user_created_by_user_id",
                table: "vnd_document");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_user_curator_developer_id",
                table: "vnd_document");

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "revision_changed_date",
                value: new DateOnly(2019, 2, 1));

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_record_users_responsible_user_id",
                table: "vnd_actualization_record",
                column: "responsible_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_request_users_decided_by_user_id",
                table: "vnd_actualization_request",
                column: "decided_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_request_users_requested_by_user_id",
                table: "vnd_actualization_request",
                column: "requested_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_approval_stage_users_approver_user_id",
                table: "vnd_approval_stage",
                column: "approver_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_users_actualization_responsible_user_id",
                table: "vnd_document",
                column: "actualization_responsible_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_users_created_by_user_id",
                table: "vnd_document",
                column: "created_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_users_curator_developer_id",
                table: "vnd_document",
                column: "curator_developer_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_record_users_responsible_user_id",
                table: "vnd_actualization_record");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_request_users_decided_by_user_id",
                table: "vnd_actualization_request");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_actualization_request_users_requested_by_user_id",
                table: "vnd_actualization_request");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_approval_stage_users_approver_user_id",
                table: "vnd_approval_stage");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_users_actualization_responsible_user_id",
                table: "vnd_document");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_users_created_by_user_id",
                table: "vnd_document");

            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_users_curator_developer_id",
                table: "vnd_document");

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "revision_changed_date",
                value: null);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_record_user_responsible_user_id",
                table: "vnd_actualization_record",
                column: "responsible_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_request_user_decided_by_user_id",
                table: "vnd_actualization_request",
                column: "decided_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_actualization_request_user_requested_by_user_id",
                table: "vnd_actualization_request",
                column: "requested_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_approval_stage_user_approver_user_id",
                table: "vnd_approval_stage",
                column: "approver_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_user_actualization_responsible_user_id",
                table: "vnd_document",
                column: "actualization_responsible_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_user_created_by_user_id",
                table: "vnd_document",
                column: "created_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_user_curator_developer_id",
                table: "vnd_document",
                column: "curator_developer_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
