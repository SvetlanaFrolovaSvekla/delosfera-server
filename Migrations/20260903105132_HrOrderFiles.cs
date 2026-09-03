using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class HrOrderFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_order_file",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_order_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_order_file_file_attachments_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hr_order_file_hr_order_order_id",
                        column: x => x.order_id,
                        principalTable: "hr_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_file_file_id",
                table: "hr_order_file",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_file_order_id",
                table: "hr_order_file",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_order_file_order_id_file_id",
                table: "hr_order_file",
                columns: new[] { "order_id", "file_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_order_file");
        }
    }
}
