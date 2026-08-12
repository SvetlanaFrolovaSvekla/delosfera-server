using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementTender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_tender",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    reg_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_limited = table.Column<bool>(type: "boolean", nullable: false),
                    published_on = table.Column<DateOnly>(type: "date", nullable: true),
                    submission_deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: true),
                    commission_order_number = table.Column<string>(type: "text", nullable: true),
                    commission_order_date = table.Column<DateOnly>(type: "date", nullable: true),
                    previous_tender_id = table.Column<int>(type: "integer", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_tender", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_tender_procurement_request_request_id",
                        column: x => x.request_id,
                        principalTable: "procurement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_tender_procurement_tender_previous_tender_id",
                        column: x => x.previous_tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_commission_member",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tender_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    is_board_member = table.Column<bool>(type: "boolean", nullable: false),
                    is_accountant = table.Column<bool>(type: "boolean", nullable: false),
                    attended_opening = table.Column<bool>(type: "boolean", nullable: false),
                    dissenting_opinion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_commission_member", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_commission_member_tenders_tender_id",
                        column: x => x.tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_commission_member_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_tender_bid",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tender_id = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    submitted_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_late = table.Column<bool>(type: "boolean", nullable: false),
                    is_admitted = table.Column<bool>(type: "boolean", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: true),
                    specification = table.Column<string>(type: "text", nullable: true),
                    is_winner = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_tender_bid", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_tender_bid_procurement_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_tender_bid_tenders_tender_id",
                        column: x => x.tender_id,
                        principalTable: "procurement_tender",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_commission_member_tender_id_user_id",
                table: "procurement_commission_member",
                columns: new[] { "tender_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_commission_member_user_id",
                table: "procurement_commission_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_previous_tender_id",
                table: "procurement_tender",
                column: "previous_tender_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_request_id",
                table: "procurement_tender",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_bid_supplier_id",
                table: "procurement_tender_bid",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_bid_tender_id_supplier_id",
                table: "procurement_tender_bid",
                columns: new[] { "tender_id", "supplier_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_commission_member");

            migrationBuilder.DropTable(
                name: "procurement_tender_bid");

            migrationBuilder.DropTable(
                name: "procurement_tender");
        }
    }
}
