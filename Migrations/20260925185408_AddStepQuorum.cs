using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddStepQuorum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quorum",
                table: "route_template_step",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quorum",
                table: "route_step",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "quorum",
                table: "route_template_step");

            migrationBuilder.DropColumn(
                name: "quorum",
                table: "route_step");
        }
    }
}
