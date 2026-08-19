using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class DirectoryPositionAndUnitAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "org_unit_attribute",
                table: "directory_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "position_attribute",
                table: "directory_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Уже заведённой настройке проставляем атрибуты Active Directory: пустое
            // значение означало бы, что должность и отдел перестали подтягиваться.
            migrationBuilder.Sql(
                "update directory_settings set position_attribute = 'title' where position_attribute = ''");
            migrationBuilder.Sql(
                "update directory_settings set org_unit_attribute = 'department' where org_unit_attribute = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "org_unit_attribute",
                table: "directory_settings");

            migrationBuilder.DropColumn(
                name: "position_attribute",
                table: "directory_settings");
        }
    }
}
