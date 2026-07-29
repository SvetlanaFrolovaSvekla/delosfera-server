using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRubricToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_document_dictionary_rubric_rubric_id",
                table: "vnd_document");

            migrationBuilder.DropIndex(
                name: "ix_vnd_document_rubric_id",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "rubric_id",
                table: "vnd_document");

            migrationBuilder.CreateTable(
                name: "vnd_rubric",
                columns: table => new
                {
                    rubric_id = table.Column<int>(type: "integer", nullable: false),
                    vnd_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_rubric", x => new { x.rubric_id, x.vnd_id });
                    table.ForeignKey(
                        name: "fk_vnd_rubric_dictionary_rubric_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "dictionary_rubric",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vnd_rubric_vnd_document_vnd_id",
                        column: x => x.vnd_id,
                        principalTable: "vnd_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "vnd_rubric",
                columns: new[] { "rubric_id", "vnd_id" },
                values: new object[,]
                {
                    { 5, 1 },
                    { 5, 2 },
                    { 7, 4 },
                    { 11, 3 },
                    { 11, 5 },
                    { 15, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_rubric_vnd_id",
                table: "vnd_rubric",
                column: "vnd_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_rubric");

            migrationBuilder.AddColumn<int>(
                name: "rubric_id",
                table: "vnd_document",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                column: "rubric_id",
                value: 5);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                column: "rubric_id",
                value: 5);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                column: "rubric_id",
                value: 15);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                column: "rubric_id",
                value: 7);

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                column: "rubric_id",
                value: 11);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_rubric_id",
                table: "vnd_document",
                column: "rubric_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_document_dictionary_rubric_rubric_id",
                table: "vnd_document",
                column: "rubric_id",
                principalTable: "dictionary_rubric",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
