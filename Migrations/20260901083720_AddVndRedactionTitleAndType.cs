using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddVndRedactionTitleAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "title_en",
                table: "vnd_redaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_kg",
                table: "vnd_redaction",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_ru",
                table: "vnd_redaction",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "type_id",
                table: "vnd_redaction",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_vnd_redaction_type_id",
                table: "vnd_redaction",
                column: "type_id");

            migrationBuilder.Sql(@"
                UPDATE vnd_redaction r
                SET title_ru = d.title_ru,
                    title_en = d.title_en,
                    title_kg = d.title_kg,
                    type_id = d.type_id
                FROM vnd_document d
                WHERE r.vnd_id = d.id;
            ");

            migrationBuilder.AddForeignKey(
                name: "fk_vnd_redaction_dictionary_type_vnd_type_id",
                table: "vnd_redaction",
                column: "type_id",
                principalTable: "dictionary_type_vnd",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vnd_redaction_dictionary_type_vnd_type_id",
                table: "vnd_redaction");

            migrationBuilder.DropIndex(
                name: "ix_vnd_redaction_type_id",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_en",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_kg",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "title_ru",
                table: "vnd_redaction");

            migrationBuilder.DropColumn(
                name: "type_id",
                table: "vnd_redaction");
        }
    }
}
