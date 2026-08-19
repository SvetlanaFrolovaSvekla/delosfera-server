using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_supplier",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    inn = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    director_name = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    is_reliable = table.Column<bool>(type: "boolean", nullable: true),
                    reliability_checked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    has_tax_clearance = table.Column<bool>(type: "boolean", nullable: false),
                    has_social_fund_clearance = table.Column<bool>(type: "boolean", nullable: false),
                    is_affiliated = table.Column<bool>(type: "boolean", nullable: false),
                    is_blacklisted = table.Column<bool>(type: "boolean", nullable: false),
                    blacklist_reason = table.Column<string>(type: "text", nullable: true),
                    blacklisted_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_supplier", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procurement_proposal",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    delivery_days = table.Column<int>(type: "integer", nullable: true),
                    warranty_months = table.Column<int>(type: "integer", nullable: true),
                    payment_terms = table.Column<string>(type: "text", nullable: true),
                    specification = table.Column<string>(type: "text", nullable: true),
                    attachment_id = table.Column<int>(type: "integer", nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    meets_requirements = table.Column<bool>(type: "boolean", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    is_winner = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_proposal", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_proposal_procurement_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "procurement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_proposal_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_proposal_request_id_supplier_id",
                table: "procurement_proposal",
                columns: new[] { "request_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_proposal_supplier_id",
                table: "procurement_proposal",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_supplier_inn",
                table: "procurement_supplier",
                column: "inn",
                unique: true,
                filter: "inn IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_supplier_is_blacklisted",
                table: "procurement_supplier",
                column: "is_blacklisted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_proposal");

            migrationBuilder.DropTable(
                name: "procurement_supplier");
        }
    }
}
