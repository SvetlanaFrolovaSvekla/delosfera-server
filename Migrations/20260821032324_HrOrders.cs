using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class HrOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_order",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reg_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    basis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source_sz_id = table.Column<int>(type: "integer", nullable: true),
                    cancels_order_id = table.Column<int>(type: "integer", nullable: true),
                    signer_user_id = table.Column<int>(type: "integer", nullable: true),
                    signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acknowledgement_sheet_id = table.Column<int>(type: "integer", nullable: true),
                    nomenclature_case_id = table.Column<int>(type: "integer", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_order", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_order_dictionary_nomenclature_case_nomenclature_case_id",
                        column: x => x.nomenclature_case_id,
                        principalTable: "dictionary_nomenclature_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_hr_order_hr_order_cancels_order_id",
                        column: x => x.cancels_order_id,
                        principalTable: "hr_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hr_order_users_signer_user_id",
                        column: x => x.signer_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "hr_order_employee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    full_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    position_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    unit_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    field_values = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_order_employee", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_order_employee_hr_order_order_id",
                        column: x => x.order_id,
                        principalTable: "hr_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hr_order_employee_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42 });

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_cancels_order_id",
                table: "hr_order",
                column: "cancels_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_kind",
                table: "hr_order",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_nomenclature_case_id",
                table: "hr_order",
                column: "nomenclature_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_signer_user_id",
                table: "hr_order",
                column: "signer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_status_order_date",
                table: "hr_order",
                columns: new[] { "status", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_year_reg_number",
                table: "hr_order",
                columns: new[] { "year", "reg_number" },
                unique: true,
                filter: "reg_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_employee_order_id_user_id",
                table: "hr_order_employee",
                columns: new[] { "order_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_employee_user_id",
                table: "hr_order_employee",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_order_employee");

            migrationBuilder.DropTable(
                name: "hr_order");

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 1,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 });

            migrationBuilder.UpdateData(
                table: "role",
                keyColumn: "id",
                keyValue: 4,
                column: "permission_codes",
                value: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 });
        }
    }
}
