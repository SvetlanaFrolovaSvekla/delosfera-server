using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SeedUsersAndRolesInConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_role_user_user_id",
                table: "user_role");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "user_role",
                newName: "users_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_role_user_id",
                table: "user_role",
                newName: "ix_user_role_users_id");

            migrationBuilder.InsertData(
                table: "dictionary_user_group",
                columns: new[] { "id", "created_at", "title_en", "title_kg", "title_ru", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND Approvers", "ВНДди макулдаштыруучулар", "Согласующие ВНД", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND Editors", "ВНД редакторлору", "Редакторы ВНД", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "IT Administrators", "ИТ-администраторлор", "ИТ-администраторы", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "user",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "last_login_at", "org_unit_id", "password_hash", "position_id", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "aosmonov@keremetbank.kg", "Азамат Осмонов", true, null, 26, "AQAAAAEAAYagAAAAELaBsuyKeMFxAB+MULrtZ9MjkT9t5fx0pas/Ozvz63EziFaKREY6cggdvLgNVCY6ag==", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "gasanova@keremetbank.kg", "Гульнара Асанова", true, null, 34, "AQAAAAEAAYagAAAAEBc570K6jxp9Cgl9z7O6LPAv1vSm0hcGL8/DUTnhv75jDPRzcFa8bfagsMXikWWvug==", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "btoktosunova@keremetbank.kg", "Бермет Токтосунова", true, null, 33, "AQAAAAEAAYagAAAAENcTGN+pr9GDKyGE9w4K5jKdimigO7jzpb+UKIoPOd/ZgnO50Hn1ffr8AYbpFRSf5Q==", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "timanaliev@keremetbank.kg", "Тимур Иманалиев", true, null, 35, "AQAAAAEAAYagAAAAEP0K+N392Olc2GDbaBBKmr+iMP2+9/8p4xm/bMhVOsFGUFBw+56Uwfncnq3D3ioFSg==", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "esydykov@keremetbank.kg", "Эрлан Сыдыков", true, null, 36, "AQAAAAEAAYagAAAAEPYYwYIZGmUKII4wk3DdX7VxFfSsdPG1Yxtoaq+OtZIKMC2wDMTQnfAxOfngvOp7Zg==", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "amamatova@keremetbank.kg", "Айгуль Маматова", true, null, 37, "AQAAAAEAAYagAAAAEJsegtPXOp1+XnMfDYn+/my6rUZeZP/mc11xAOs3fgVFJxu8xfdFoQKxjULd0pNp3g==", 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "bkadyrov@keremetbank.kg", "Бакыт Кадыров", true, null, 38, "AQAAAAEAAYagAAAAEMIfWpsAG+CZv+Ne2uZBrKf1Wce56WoQWK1QkEJ7Mlhy6/CbH7VcSIG22izU03JewQ==", 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "nabdiev@keremetbank.kg", "Нурлан Абдиев", true, null, 3, "AQAAAAEAAYagAAAAEJY34tOj9D2kQzEPbP8IzIAcRXY9LG1WvfFAF/x8gXYIJCpkPbQ/pTWvJYwYXEy3yg==", 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ajumaeva@keremetbank.kg", "Алия Жумаева", true, null, 32, "AQAAAAEAAYagAAAAEHqpAAB4JDlfYHk91LymTPuh0fK0lTjEyIqsUskrPHlify//ciM1Pgj+tHYkf8i4Mg==", 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "rormonov@keremetbank.kg", "Руслан Ормонов", true, null, 39, "AQAAAAEAAYagAAAAEOWaZvoD8t9WLi3j+9ROOKm4VfhoSC41MG0f6nol0WCiRsjmAn6uhIXOuoxdEyiU3w==", 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "sibraeva@keremetbank.kg", "Салтанат Ибраева", true, null, 38, "AQAAAAEAAYagAAAAEFbfx4mqL8rO9aGfQnkhjR81i+g55Np6sSjzVvt8+zh2hhSvvEeR4cx6k7aq32NVWw==", 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "dusenov@keremetbank.kg", "Данияр Усенов", true, null, 4, "AQAAAAEAAYagAAAAEKHOs8yYxWNUVqDlX7az+fTMoaJ+etAefAuscQcbQvaG+3myOORWgkwziGQQ0MNVHQ==", 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@keremetbank.kg", "Администратор СЭД", true, null, 3, "AQAAAAEAAYagAAAAEJ22NmtcxUmIhlc2h5MYf+9ras0f2x67OwOIA1JzqpE0EqB10wr/7yYZD1HvYlcEsA==", 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "user_group_member",
                columns: new[] { "user_group_id", "users_id" },
                values: new object[,]
                {
                    { 1, 5 },
                    { 1, 6 },
                    { 1, 7 },
                    { 1, 11 },
                    { 1, 12 },
                    { 2, 3 },
                    { 2, 5 },
                    { 2, 6 },
                    { 2, 7 },
                    { 2, 11 },
                    { 2, 12 },
                    { 3, 8 },
                    { 3, 13 }
                });

            migrationBuilder.InsertData(
                table: "user_role",
                columns: new[] { "roles_id", "users_id" },
                values: new object[,]
                {
                    { 1, 3 },
                    { 1, 13 },
                    { 2, 1 },
                    { 2, 2 },
                    { 2, 4 },
                    { 2, 8 },
                    { 2, 9 },
                    { 2, 10 },
                    { 3, 5 },
                    { 3, 6 },
                    { 3, 7 },
                    { 3, 11 },
                    { 3, 12 },
                    { 4, 3 }
                });

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_user_users_id",
                table: "user_role",
                column: "users_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_role_user_users_id",
                table: "user_role");

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 1, 5 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 1, 6 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 1, 7 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 1, 11 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 1, 12 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 3 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 5 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 6 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 7 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 11 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 2, 12 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 3, 8 });

            migrationBuilder.DeleteData(
                table: "user_group_member",
                keyColumns: new[] { "user_group_id", "users_id" },
                keyValues: new object[] { 3, 13 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 1, 13 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 4 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 8 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 9 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 2, 10 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 5 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 6 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 7 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 11 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 3, 12 });

            migrationBuilder.DeleteData(
                table: "user_role",
                keyColumns: new[] { "roles_id", "users_id" },
                keyValues: new object[] { 4, 3 });

            migrationBuilder.DeleteData(
                table: "dictionary_user_group",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "dictionary_user_group",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "dictionary_user_group",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "user",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.RenameColumn(
                name: "users_id",
                table: "user_role",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_role_users_id",
                table: "user_role",
                newName: "ix_user_role_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_user_user_id",
                table: "user_role",
                column: "user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
