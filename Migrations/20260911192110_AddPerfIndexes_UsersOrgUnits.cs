using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfIndexes_UsersOrgUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_user_full_name",
                table: "user",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "ix_user_is_active",
                table: "user",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_full_name",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_user_is_active",
                table: "user");
        }
    }
}
