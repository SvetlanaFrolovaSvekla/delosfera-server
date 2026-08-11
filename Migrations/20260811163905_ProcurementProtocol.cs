using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_protocol",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    reg_number = table.Column<string>(type: "text", nullable: true),
                    protocol_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    method_title = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    initiator_unit_title = table.Column<string>(type: "text", nullable: true),
                    main_supplier_id = table.Column<int>(type: "integer", nullable: true),
                    main_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    reserve_supplier_id = table.Column<int>(type: "integer", nullable: true),
                    reserve_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    budget_note = table.Column<string>(type: "text", nullable: true),
                    expert_opinion = table.Column<string>(type: "text", nullable: true),
                    dissenting_opinion = table.Column<string>(type: "text", nullable: true),
                    recommendations = table.Column<string>(type: "text", nullable: true),
                    selection_basis = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_protocol", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_procurement_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "procurement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_suppliers_main_supplier_id",
                        column: x => x.main_supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_suppliers_reserve_supplier_id",
                        column: x => x.reserve_supplier_id,
                        principalTable: "procurement_supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_protocol_row",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    protocol_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    supplier_title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    supplier_inn = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    specification = table.Column<string>(type: "text", nullable: true),
                    delivery_terms = table.Column<string>(type: "text", nullable: true),
                    payment_terms = table.Column<string>(type: "text", nullable: true),
                    initiator_conclusion = table.Column<string>(type: "text", nullable: false),
                    is_winner = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_protocol_row", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_row_procurement_protocol_protocol_id",
                        column: x => x.protocol_id,
                        principalTable: "procurement_protocol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "procurement_protocol_signature",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    protocol_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_protocol_signature", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_signature_procurement_protocol_protoco",
                        column: x => x.protocol_id,
                        principalTable: "procurement_protocol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_protocol_signature_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_main_supplier_id",
                table: "procurement_protocol",
                column: "main_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_request_id",
                table: "procurement_protocol",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_reserve_supplier_id",
                table: "procurement_protocol",
                column: "reserve_supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_row_protocol_id_order",
                table: "procurement_protocol_row",
                columns: new[] { "protocol_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_signature_protocol_id_role_revoked",
                table: "procurement_protocol_signature",
                columns: new[] { "protocol_id", "role", "revoked" });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_protocol_signature_user_id",
                table: "procurement_protocol_signature",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_protocol_row");

            migrationBuilder.DropTable(
                name: "procurement_protocol_signature");

            migrationBuilder.DropTable(
                name: "procurement_protocol");
        }
    }
}
