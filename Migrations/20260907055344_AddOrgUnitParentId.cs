using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgUnitParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                column: "parent_id",
                value: 1);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                column: "parent_id",
                value: 5);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                column: "parent_id",
                value: 13);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                column: "parent_id",
                value: 13);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                column: "parent_id",
                value: 13);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 57,
                column: "parent_id",
                value: 35);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 60,
                column: "parent_id",
                value: 10);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 61,
                column: "parent_id",
                value: 145);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 62,
                column: "parent_id",
                value: 43);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 63,
                column: "parent_id",
                value: 8);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 64,
                column: "parent_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 65,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 66,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 67,
                column: "parent_id",
                value: 164);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 68,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 69,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 70,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 71,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 72,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 73,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 74,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 75,
                column: "parent_id",
                value: 51);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 76,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 77,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 78,
                column: "parent_id",
                value: 8);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 79,
                column: "parent_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 80,
                column: "parent_id",
                value: 43);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 81,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 82,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 83,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 84,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 85,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 86,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 87,
                column: "parent_id",
                value: 51);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 89,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 90,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 91,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 92,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 93,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 94,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 95,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 96,
                column: "parent_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 97,
                column: "parent_id",
                value: 13);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 98,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 99,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 100,
                column: "parent_id",
                value: 43);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 101,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 103,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 104,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 105,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 106,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 107,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 108,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 109,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 110,
                column: "parent_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 111,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 112,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 114,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 115,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 116,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 117,
                column: "parent_id",
                value: 3);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 118,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 119,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 120,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 121,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 122,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 123,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 125,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 126,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 127,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 128,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 129,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 130,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 131,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 132,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 133,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 135,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 136,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 138,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 139,
                column: "parent_id",
                value: 156);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 140,
                column: "parent_id",
                value: 159);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 141,
                column: "parent_id",
                value: 113);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 142,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 143,
                column: "parent_id",
                value: 88);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 144,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 146,
                column: "parent_id",
                value: 102);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 147,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 148,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 149,
                column: "parent_id",
                value: 154);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 150,
                column: "parent_id",
                value: 151);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 152,
                column: "parent_id",
                value: 124);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 153,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 155,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 158,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 160,
                column: "parent_id",
                value: 134);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 164,
                column: "parent_id",
                value: 59);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 2,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 6,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 14,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 16,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 17,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 57,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 60,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 61,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 62,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 63,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 64,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 65,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 66,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 67,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 68,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 69,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 70,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 71,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 72,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 73,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 74,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 75,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 76,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 77,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 78,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 79,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 80,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 81,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 82,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 83,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 84,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 85,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 86,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 87,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 89,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 90,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 91,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 92,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 93,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 94,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 95,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 96,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 97,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 98,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 99,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 100,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 101,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 103,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 104,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 105,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 106,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 107,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 108,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 109,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 110,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 111,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 112,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 114,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 115,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 116,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 117,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 118,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 119,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 120,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 121,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 122,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 123,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 125,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 126,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 127,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 128,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 129,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 130,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 131,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 132,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 133,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 135,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 136,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 138,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 139,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 140,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 141,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 142,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 143,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 144,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 146,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 147,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 148,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 149,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 150,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 152,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 153,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 155,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 158,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 160,
                column: "parent_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "dictionary_organization_unit",
                keyColumn: "id",
                keyValue: 164,
                column: "parent_id",
                value: null);
        }
    }
}
