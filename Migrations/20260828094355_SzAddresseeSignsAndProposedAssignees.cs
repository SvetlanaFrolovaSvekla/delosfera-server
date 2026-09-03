using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Снимок role.permission_codes вырезан — четвёртый раз. Права на новые
    /// разделы раздаёт RolePermissionDefaults при старте.
    ///
    /// Миграция заводит таблицу предложенных исполнителей. Колонка signer_user_id
    /// остаётся: под ней записки, заведённые до объединения ролей, когда подписант
    /// был отдельным человеком.
    /// </remarks>
    public partial class SzAddresseeSignsAndProposedAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sz_proposed_assignees",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sz_document_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sz_proposed_assignees", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_proposed_assignees_sz_documents_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_proposed_assignees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });



            migrationBuilder.CreateIndex(
                name: "ix_sz_proposed_assignees_sz_document_id",
                table: "sz_proposed_assignees",
                column: "sz_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_proposed_assignees_user_id",
                table: "sz_proposed_assignees",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sz_proposed_assignees");


        }
    }
}
