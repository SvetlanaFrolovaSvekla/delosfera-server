using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileFrolovaMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: 52);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "vnd_coordination_default_approver",
                keyColumn: "id",
                keyValue: 4,
                column: "org_unit_id",
                value: 33);
        }
    }
}
