using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>vnd_approval_norm_settings - справочник нормативов согласования редакции ВНД
    /// по умолчанию (первичное согласование 7 д., после внесённых изменений 4 д., финальная
    /// выдержка 3 д.; значения в минутах).
    ///
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924150000_AddVndApprovalNormSettings")]
    public partial class AddVndApprovalNormSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_approval_norm_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    primary_deadline_minutes = table.Column<int>(type: "integer", nullable: false),
                    repeat_deadline_minutes = table.Column<int>(type: "integer", nullable: false),
                    final_hold_deadline_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_approval_norm_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "vnd_approval_norm_settings",
                columns: new[] { "id", "created_at", "final_hold_deadline_minutes", "primary_deadline_minutes", "repeat_deadline_minutes", "updated_at" },
                // Типы колонок указаны явно: у миграции нет Designer-файла (целевой модели),
                // и без них EF не может сгенерировать INSERT.
                columnTypes: new[] { "integer", "timestamp with time zone", "integer", "integer", "integer", "timestamp with time zone" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4320, 10080, 5760, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnd_approval_norm_settings");
        }
    }
}
