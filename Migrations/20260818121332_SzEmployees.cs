using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sz_employee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    org_unit_id = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    values_json = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_employee", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_employee_dictionary_organization_unit_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "dictionary_organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_sz_employee_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_employee_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_employee_org_unit_id",
                table: "sz_employee",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_employee_sz_document_id_sort_order",
                table: "sz_employee",
                columns: new[] { "sz_document_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_sz_employee_user_id",
                table: "sz_employee",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sz_employee");
        }
    }
}
