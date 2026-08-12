using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_contract",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    protocol_id = table.Column<int>(type: "integer", nullable: true),
                    tender_id = table.Column<int>(type: "integer", nullable: true),
                    supplier_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    signed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    delivery_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    payment_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    responsible_user_id = table.Column<int>(type: "integer", nullable: true),
                    termination_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_contract", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_contract_document_document_id",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_contract_procurement_protocols_protocol_id",
                        column: x => x.protocol_id,
                        principalTable: "procurement_protocol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_procurement_contract_procurement_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "procurement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_contract_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_contract_tenders_tender_id",
                        column: x => x.tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_procurement_contract_users_responsible_user_id",
                        column: x => x.responsible_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_delivery_act",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    act_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subject = table.Column<string>(type: "text", nullable: true),
                    approved_by_unit_head_id = table.Column<int>(type: "integer", nullable: true),
                    unit_head_approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by_curator_id = table.Column<int>(type: "integer", nullable: true),
                    curator_approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_delivery_act", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_delivery_act_procurement_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "procurement_contract",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_delivery_act_users_approved_by_curator_id",
                        column: x => x.approved_by_curator_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_delivery_act_users_approved_by_unit_head_id",
                        column: x => x.approved_by_unit_head_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "procurement_parameter",
                columns: new[] { "id", "code", "created_at", "source_note", "title_ru", "unit", "updated_at", "value" },
                values: new object[] { 6, "CuratorActApprovalThreshold", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PRC-19: акты свыше 1 млн утверждает дополнительно куратор Правления", "Порог утверждения акта курирующим членом Правления", "сом", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1000000m });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_document_id",
                table: "procurement_contract",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_protocol_id",
                table: "procurement_contract",
                column: "protocol_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_request_id",
                table: "procurement_contract",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_responsible_user_id",
                table: "procurement_contract",
                column: "responsible_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_supplier_id",
                table: "procurement_contract",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_contract_tender_id",
                table: "procurement_contract",
                column: "tender_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_delivery_act_approved_by_curator_id",
                table: "procurement_delivery_act",
                column: "approved_by_curator_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_delivery_act_approved_by_unit_head_id",
                table: "procurement_delivery_act",
                column: "approved_by_unit_head_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_delivery_act_contract_id_number",
                table: "procurement_delivery_act",
                columns: new[] { "contract_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_delivery_act");

            migrationBuilder.DropTable(
                name: "procurement_contract");

            migrationBuilder.DeleteData(
                table: "procurement_parameter",
                keyColumn: "id",
                keyValue: 6);
        }
    }
}
