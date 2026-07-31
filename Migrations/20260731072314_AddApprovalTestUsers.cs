using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalTestUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "user",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "last_login_at", "org_unit_id", "password_hash", "position_id", "updated_at" },
                values: new object[,]
                {
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "anuruev@keremetbank.kg", "Айбек Нуруев", true, null, 28, "AQAAAAEAAYagAAAAEHASH_ANURUEV==", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "jesenova@keremetbank.kg", "Жаныл Эсенова", true, null, 5, "AQAAAAEAAYagAAAAEHASH_JESENOVA==", 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "mbekbolotov@keremetbank.kg", "Марат Бекболотов", true, null, 34, "AQAAAAEAAYagAAAAEHASH_MBEKBOLOTOV==", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "noskonov@keremetbank.kg", "Нурбек Осконов", true, null, 33, "AQAAAAEAAYagAAAAEHASH_NOSKONOV==", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "atokoeva@keremetbank.kg", "Айнура Токоева", true, null, 3, "AQAAAAEAAYagAAAAEHASH_ATOKOEVA==", 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "dpetrov@keremetbank.kg", "Данил Петров", true, null, 37, "AQAAAAEAAYagAAAAEHASH_DPETROV==", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "user_role",
                columns: new[] { "roles_id", "users_id" },
                values: new object[,]
                {
                    { 2, 18 },
                    { 2, 19 },
                    { 3, 14 },
                    { 3, 15 },
                    { 3, 16 },
                    { 3, 17 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 18 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 19 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 14 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 15 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 16 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 17 });

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 19);
        }
    }
}
