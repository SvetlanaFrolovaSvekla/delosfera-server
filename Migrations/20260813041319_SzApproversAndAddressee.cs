using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzApproversAndAddressee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "addressee_decision",
                table: "sz_document",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "addressee_decision_at",
                table: "sz_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "addressee_decision_by_user_id",
                table: "sz_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "addressee_user_id",
                table: "sz_document",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "approval_is_parallel",
                table: "sz_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "sz_approver",
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
                    table.PrimaryKey("pk_sz_approver", x => x.id);
                    table.ForeignKey(
                        name: "fk_sz_approver_sz_document_sz_document_id",
                        column: x => x.sz_document_id,
                        principalTable: "sz_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sz_approver_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sz_document_addressee_user_id",
                table: "sz_document",
                column: "addressee_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sz_approver_sz_document_id_user_id",
                table: "sz_approver",
                columns: new[] { "sz_document_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sz_approver_user_id",
                table: "sz_approver",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sz_document_users_addressee_user_id",
                table: "sz_document",
                column: "addressee_user_id",
                principalTable: "user",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sz_document_users_addressee_user_id",
                table: "sz_document");

            migrationBuilder.DropTable(
                name: "sz_approver");

            migrationBuilder.DropIndex(
                name: "ix_sz_document_addressee_user_id",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "addressee_decision",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "addressee_decision_at",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "addressee_decision_by_user_id",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "addressee_user_id",
                table: "sz_document");

            migrationBuilder.DropColumn(
                name: "approval_is_parallel",
                table: "sz_document");
        }
    }
}
