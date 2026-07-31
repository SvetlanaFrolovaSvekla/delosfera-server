using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApprovalTestUserPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEFSX5Y9mFPKRXkdc0ZpOic9JgLOmCyM6M3nS1F25N6k6Wkq6YRY/5/661p4kJbLiIw==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEJqdeTyPiLGevQVuajcuy3on8zX2JUZAcez6WwYuW546rr7DEhBtMFYOr/lZHRx5aw==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEJpz1AWMncL0sXvrT8aX/qcQA8GttDT/hxPkJpF7YziJRmwmvNGWWtFA0jDTlE/rxw==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEKXQ0AW10isOhlDcbGPT3gKxa/FaM3gtb2FxhkLBzurisRAXL0HeBBusit2wDqDOkA==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEIPjSgpvjJwzYCkxoEjkiZaopA0lLwFnDpT75Vvr78Y3YX3VGFQKXmXpy2F57M2VJQ==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHCfve+vcccRWGbiuG7dVDwSpv4ep13q1yGPUnZH66qz101wPZ5GV3becS/qgn4PHA==");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 14,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_ANURUEV==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 15,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_JESENOVA==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 16,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_MBEKBOLOTOV==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 17,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_NOSKONOV==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 18,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_ATOKOEVA==");

            migrationBuilder.UpdateData(
                table: "user",
                keyColumn: "id",
                keyValue: 19,
                column: "password_hash",
                value: "AQAAAAEAAYagAAAAEHASH_DPETROV==");
        }
    }
}
