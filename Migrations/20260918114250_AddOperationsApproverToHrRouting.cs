using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsApproverToHrRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "operations_approver_user_id",
                table: "hr_routing_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "operations_fallback_user_id",
                table: "hr_routing_settings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "operations_approver_user_id",
                table: "hr_routing_settings");

            migrationBuilder.DropColumn(
                name: "operations_fallback_user_id",
                table: "hr_routing_settings");
        }
    }
}
