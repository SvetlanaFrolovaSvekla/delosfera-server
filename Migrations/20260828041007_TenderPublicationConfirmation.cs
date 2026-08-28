using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class TenderPublicationConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "publication_confirmed_at",
                table: "procurement_tender",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publication_confirmed_by_id",
                table: "procurement_tender",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publication_confirmed_by_user_id",
                table: "procurement_tender",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_at",
                table: "procurement_tender",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_procurement_tender_publication_confirmed_by_id",
                table: "procurement_tender",
                column: "publication_confirmed_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_procurement_tender_users_publication_confirmed_by_id",
                table: "procurement_tender",
                column: "publication_confirmed_by_id",
                principalTable: "user",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_procurement_tender_users_publication_confirmed_by_id",
                table: "procurement_tender");

            migrationBuilder.DropIndex(
                name: "ix_procurement_tender_publication_confirmed_by_id",
                table: "procurement_tender");

            migrationBuilder.DropColumn(
                name: "publication_confirmed_at",
                table: "procurement_tender");

            migrationBuilder.DropColumn(
                name: "publication_confirmed_by_id",
                table: "procurement_tender");

            migrationBuilder.DropColumn(
                name: "publication_confirmed_by_user_id",
                table: "procurement_tender");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "procurement_tender");
        }
    }
}
