using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitHeadAndCurator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dictionary_organization_unit_dictionary_organization_unit_p",
                table: "dictionary_organization_unit");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "vnd_document",
                newName: "title_ru");

            migrationBuilder.AddColumn<string>(
                name: "title_en",
                table: "vnd_document",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_kg",
                table: "vnd_document",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "curator_user_id",
                table: "dictionary_organization_unit",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "head_user_id",
                table: "dictionary_organization_unit",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 7,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 21,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 22,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 23,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 24,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 25,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 26,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 27,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 28,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 29,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 30,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 31,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 32,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 33,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 34,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 35,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 36,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 37,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 38,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 39,
                columns: new[] { "curator_user_id", "head_user_id" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "vnd_document",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "title_en", "title_kg" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_organization_unit_curator_user_id",
                table: "dictionary_organization_unit",
                column: "curator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_organization_unit_head_user_id",
                table: "dictionary_organization_unit",
                column: "head_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_dictionary_organization_unit_dictionary_organization_unit_p",
                table: "dictionary_organization_unit",
                column: "parent_id",
                principalTable: "dictionary_organization_unit",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_dictionary_organization_unit_users_curator_user_id",
                table: "dictionary_organization_unit",
                column: "curator_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_dictionary_organization_unit_users_head_user_id",
                table: "dictionary_organization_unit",
                column: "head_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dictionary_organization_unit_dictionary_organization_unit_p",
                table: "dictionary_organization_unit");

            migrationBuilder.DropForeignKey(
                name: "fk_dictionary_organization_unit_users_curator_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.DropForeignKey(
                name: "fk_dictionary_organization_unit_users_head_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.DropIndex(
                name: "ix_dictionary_organization_unit_curator_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.DropIndex(
                name: "ix_dictionary_organization_unit_head_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.DropColumn(
                name: "title_en",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "title_kg",
                table: "vnd_document");

            migrationBuilder.DropColumn(
                name: "curator_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.DropColumn(
                name: "head_user_id",
                table: "dictionary_organization_unit");

            migrationBuilder.RenameColumn(
                name: "title_ru",
                table: "vnd_document",
                newName: "name");

            migrationBuilder.AddForeignKey(
                name: "fk_dictionary_organization_unit_dictionary_organization_unit_p",
                table: "dictionary_organization_unit",
                column: "parent_id",
                principalTable: "dictionary_organization_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
