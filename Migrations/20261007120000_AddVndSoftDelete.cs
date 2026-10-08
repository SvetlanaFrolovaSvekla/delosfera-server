using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Мягкое удаление ВНД (право DeleteAnyVnd): флаг is_deleted, время и автор удаления.
    /// Уникальность кода ВНД теперь только среди неудалённых. Миграция написана вручную (без
    /// Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20261007120000_AddVndSoftDelete")]
    public partial class AddVndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "vnd_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "vnd_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deleted_by_user_id",
                table: "vnd_document",
                type: "integer",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "ix_vnd_document_code",
                table: "vnd_document");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_code",
                table: "vnd_document",
                column: "code",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vnd_document_code",
                table: "vnd_document");

            migrationBuilder.CreateIndex(
                name: "ix_vnd_document_code",
                table: "vnd_document",
                column: "code",
                unique: true);

            migrationBuilder.DropColumn(name: "deleted_by_user_id", table: "vnd_document");
            migrationBuilder.DropColumn(name: "deleted_at", table: "vnd_document");
            migrationBuilder.DropColumn(name: "is_deleted", table: "vnd_document");
        }
    }
}
