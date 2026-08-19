using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_request",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    justification = table.Column<string>(type: "text", nullable: true),
                    subject_kind = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_affiliated = table.Column<bool>(type: "boolean", nullable: false),
                    has_budget = table.Column<bool>(type: "boolean", nullable: false),
                    plan_item = table.Column<string>(type: "text", nullable: true),
                    has_specification = table.Column<bool>(type: "boolean", nullable: false),
                    initiator_unit_id = table.Column<int>(type: "integer", nullable: true),
                    curator_user_id = table.Column<int>(type: "integer", nullable: true),
                    method_id = table.Column<int>(type: "integer", nullable: false),
                    matrix_rule_id = table.Column<int>(type: "integer", nullable: true),
                    approval_chain = table.Column<string>(type: "text", nullable: true),
                    approval_authority = table.Column<int>(type: "integer", nullable: false),
                    method_justification = table.Column<string>(type: "text", nullable: true),
                    protocol_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procurement_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_procurement_request_dictionary_organization_unit_initiator_",
                        column: x => x.initiator_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_request_dictionary_procurement_method_method_id",
                        column: x => x.method_id,
                        principalTable: "dictionary_procurement_method",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_procurement_request_document_document_id",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_procurement_request_procurement_authority_matrix_rule_matri",
                        column: x => x.matrix_rule_id,
                        principalTable: "procurement_authority_matrix_rule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_procurement_request_users_curator_user_id",
                        column: x => x.curator_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_curator_user_id",
                table: "procurement_request",
                column: "curator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_document_id",
                table: "procurement_request",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_initiator_unit_id",
                table: "procurement_request",
                column: "initiator_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_matrix_rule_id",
                table: "procurement_request",
                column: "matrix_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_procurement_request_method_id",
                table: "procurement_request",
                column: "method_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_request");
        }
    }
}
