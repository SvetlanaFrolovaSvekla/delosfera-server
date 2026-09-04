using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class HrOrderSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "hr_order",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(title, '') || ' ' || coalesce(body, '') || ' ' || coalesce(basis, '') || ' ' || coalesce(reg_number, ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_search_vector",
                table: "hr_order",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hr_order_search_vector",
                table: "hr_order");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "hr_order");
        }
    }
}
