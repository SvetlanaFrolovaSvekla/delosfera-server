using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddEditVndRequisitesPermissionAndFixLegalApprover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_coordination_default_approver_user_approver_user_id",
                table: "vnd_coordination_default_approver");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 3,
                column: "permission_codes",
                value: new[] { 1, 4, 5, 6, 10, 21, 11, 12, 16, 17, 18, 25 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 });

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                column: "approver_user_id",
                value: 16);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_coordination_default_approver_users_approver_user_id",
                table: "vnd_coordination_default_approver",
                column: "approver_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_coordination_default_approver_users_approver_user_id",
                table: "vnd_coordination_default_approver");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 3,
                column: "permission_codes",
                value: new[] { 1, 4, 5, 6, 10, 21, 11, 12, 16, 17, 18 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 });

            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 1,
                column: "approver_user_id",
                value: 2);

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_coordination_default_approver_user_approver_user_id",
                table: "vnd_coordination_default_approver",
                column: "approver_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
