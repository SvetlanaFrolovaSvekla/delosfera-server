using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class RenumberPermissionCodesUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.InsertData(
                table: "user_role",
                columns: new[] { "roles_id", "users_id" },
                values: new object[] { 3, 2 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.InsertData(
                table: "user_role",
                columns: new[] { "roles_id", "users_id" },
                values: new object[] { 2, 2 });
        }
    }
}
