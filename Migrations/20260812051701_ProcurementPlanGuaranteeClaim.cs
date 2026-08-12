using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementPlanGuaranteeClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    reg_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    violation = table.Column<string>(type: "text", nullable: false),
                    demand = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sent_on = table.Column<DateOnly>(type: "date", nullable: true),
                    response_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    answered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    response = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_claim_procurement_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "procurement_contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "procurement_guarantee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    form = table.Column<int>(type: "integer", nullable: false),
                    tender_id = table.Column<int>(type: "integer", nullable: true),
                    contract_id = table.Column<int>(type: "integer", nullable: true),
                    supplier_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    document_ref = table.Column<string>(type: "text", nullable: true),
                    returned_on = table.Column<DateOnly>(type: "date", nullable: true),
                    returned_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    is_forfeited = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_guarantee", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_guarantee_procurement_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "procurement_contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_guarantee_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_guarantee_tenders_tender_id",
                        column: x => x.tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_guarantee_users_returned_by_user_id",
                        column: x => x.returned_by_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approval_protocol = table.Column<string>(type: "text", nullable: true),
                    approved_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_plan", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procurement_plan_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plan_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    planned_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quarter = table.Column<int>(type: "integer", nullable: true),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true),
                    subject_kind = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_plan_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_plan_item_dictionary_organization_unit_org_unit",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_plan_item_procurement_plan_plan_id",
                        column: x => x.plan_id,
                        principalTable: "procurement_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_claim_contract_id",
                table: "procurement_claim",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_guarantee_contract_id",
                table: "procurement_guarantee",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_guarantee_returned_by_user_id",
                table: "procurement_guarantee",
                column: "returned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_guarantee_supplier_id",
                table: "procurement_guarantee",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_guarantee_tender_id",
                table: "procurement_guarantee",
                column: "tender_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_guarantee_valid_until_returned_on",
                table: "procurement_guarantee",
                columns: new[] { "valid_until", "returned_on" });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_plan_year",
                table: "procurement_plan",
                column: "year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_plan_item_org_unit_id",
                table: "procurement_plan_item",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_plan_item_plan_id_code",
                table: "procurement_plan_item",
                columns: new[] { "plan_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_claim");

            migrationBuilder.DropTable(
                name: "procurement_guarantee");

            migrationBuilder.DropTable(
                name: "procurement_plan_item");

            migrationBuilder.DropTable(
                name: "procurement_plan");
        }
    }
}
