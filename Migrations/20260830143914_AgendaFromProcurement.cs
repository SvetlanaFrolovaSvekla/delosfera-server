using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class AgendaFromProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source_procurement_request_id",
                table: "meeting_agenda_item",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_source_procurement_request_id",
                table: "meeting_agenda_item",
                column: "source_procurement_request_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_agenda_item_procurement_requests_source_procurement",
                table: "meeting_agenda_item",
                column: "source_procurement_request_id",
                principalTable: "procurement_request",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_agenda_item_procurement_requests_source_procurement",
                table: "meeting_agenda_item");

            migrationBuilder.DropIndex(
                name: "ix_meeting_agenda_item_source_procurement_request_id",
                table: "meeting_agenda_item");

            migrationBuilder.DropColumn(
                name: "source_procurement_request_id",
                table: "meeting_agenda_item");
        }
    }
}
