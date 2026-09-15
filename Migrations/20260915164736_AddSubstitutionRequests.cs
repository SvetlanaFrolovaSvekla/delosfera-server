using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstitutionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "substitution_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reg_number = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    initiator_user_id = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: false),
                    absent_user_id = table.Column<int>(type: "integer", nullable: true),
                    absent_name = table.Column<string>(type: "text", nullable: false),
                    absent_position = table.Column<string>(type: "text", nullable: true),
                    absent_branch = table.Column<string>(type: "text", nullable: true),
                    absent_unit_id = table.Column<int>(type: "integer", nullable: true),
                    substitute_user_id = table.Column<int>(type: "integer", nullable: true),
                    substitute_name = table.Column<string>(type: "text", nullable: false),
                    substitute_position = table.Column<string>(type: "text", nullable: true),
                    substitute_branch = table.Column<string>(type: "text", nullable: true),
                    substitute_unit_id = table.Column<int>(type: "integer", nullable: true),
                    passport_series_number = table.Column<string>(type: "text", nullable: true),
                    passport_issued_by = table.Column<string>(type: "text", nullable: true),
                    inn = table.Column<string>(type: "text", nullable: true),
                    passport_issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    passport_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    address_registration = table.Column<string>(type: "text", nullable: true),
                    address_residence = table.Column<string>(type: "text", nullable: true),
                    days_count = table.Column<int>(type: "integer", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    commission_chair_user_id = table.Column<int>(type: "integer", nullable: true),
                    commission_chair_name = table.Column<string>(type: "text", nullable: true),
                    commission_chair_position = table.Column<string>(type: "text", nullable: true),
                    handover_moment = table.Column<int>(type: "integer", nullable: false),
                    handover_on = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitution_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitution_requests_organization_units_absent_unit_id",
                        column: x => x.absent_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_requests_organization_units_substitute_unit_id",
                        column: x => x.substitute_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_requests_users_absent_user_id",
                        column: x => x.absent_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_requests_users_commission_chair_user_id",
                        column: x => x.commission_chair_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_requests_users_initiator_user_id",
                        column: x => x.initiator_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_substitution_requests_users_substitute_user_id",
                        column: x => x.substitute_user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "substitution_commission_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitution_commission_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitution_commission_members_substitution_requests_reque",
                        column: x => x.request_id,
                        principalTable: "substitution_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_substitution_commission_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_substitution_commission_members_request_id",
                table: "substitution_commission_members",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_commission_members_user_id",
                table: "substitution_commission_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_absent_unit_id",
                table: "substitution_requests",
                column: "absent_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_absent_user_id",
                table: "substitution_requests",
                column: "absent_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_commission_chair_user_id",
                table: "substitution_requests",
                column: "commission_chair_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_initiator_user_id",
                table: "substitution_requests",
                column: "initiator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_status",
                table: "substitution_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_substitute_unit_id",
                table: "substitution_requests",
                column: "substitute_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitution_requests_substitute_user_id",
                table: "substitution_requests",
                column: "substitute_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "substitution_commission_members");

            migrationBuilder.DropTable(
                name: "substitution_requests");
        }
    }
}
