using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Производственный календарь ВНД: только праздники + рабочее время банка.
    ///
    /// 1. vnd_work_calendar_day: убраны "рабочий выходной" и "сокращённый день" — такие записи
    ///    удаляются, колонка kind больше не нужна.
    /// 2. vnd_work_hours_settings — рабочее время банка (минуты от полуночи по Бишкеку), одна
    ///    запись, по умолчанию 09:00–18:00.
    ///
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924180000_VndWorkCalendarHolidaysAndBankHours")]
    public partial class VndWorkCalendarHolidaysAndBankHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 = праздник; 2 (рабочий выходной) и 3 (сокращённый день) больше не поддерживаются.
            migrationBuilder.Sql("DELETE FROM vnd_work_calendar_day WHERE kind <> 1;");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "vnd_work_calendar_day");

            migrationBuilder.CreateTable(
                name: "vnd_work_hours_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_start_minutes = table.Column<int>(type: "integer", nullable: false),
                    work_end_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_work_hours_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "vnd_work_hours_settings",
                columns: new[] { "id", "created_at", "updated_at", "work_end_minutes", "work_start_minutes" },
                // Типы колонок указаны явно: у миграции нет Designer-файла.
                columnTypes: new[] { "integer", "timestamp with time zone", "timestamp with time zone", "integer", "integer" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1080, 540 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vnd_work_hours_settings");

            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "vnd_work_calendar_day",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
