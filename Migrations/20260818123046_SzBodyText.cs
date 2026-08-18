using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <inheritdoc />
    public partial class SzBodyText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "body_text",
                table: "sz_document",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                // До этой миграции текст записки был простой строкой и разметки не
                // содержал — переносим его как есть. Без этого вектор поиска у всех
                // прежних записок соберётся пустым, и они перестанут находиться.
                "UPDATE sz_document SET body_text = body WHERE body IS NOT NULL AND body_text IS NULL;");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "sz_document",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(body_text, '') || ' ' || coalesce(execution_resolution, ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "to_tsvector('russian', coalesce(body, '') || ' ' || coalesce(execution_resolution, ''))",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "body_text",
                table: "sz_document");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "sz_document",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('russian', coalesce(body, '') || ' ' || coalesce(execution_resolution, ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "to_tsvector('russian', coalesce(body_text, '') || ' ' || coalesce(execution_resolution, ''))",
                oldStored: true);
        }
    }
}
