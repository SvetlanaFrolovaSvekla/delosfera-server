using System;
using delosfera_server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace delosfera_server.Migrations
{
    /// <summary>Сроки согласования редакций ВНД — только в рабочее время (пн–пт 09:00–18:00 по
    /// Бишкеку, без праздников).
    ///
    /// 1. vnd_work_calendar_day — справочник "Производственный календарь" (праздники, рабочие
    ///    выходные, сокращённые дни), ведёт главный редактор.
    /// 2. vnd_approval_process: сроки фаз теперь хранятся (primary/repeat/final_hold_deadline_at)
    ///    вместо вычисления "старт + минуты", и флаг uses_working_time. Уже идущие процессы
    ///    остаются календарными (uses_working_time = false) — сроки им заполняются тем же
    ///    "старт + минуты", ничего не сдвигается.
    /// 3. vnd_approval_norm_settings: нормативы по умолчанию переводятся из календарных минут
    ///    (1 д. = 1440) в рабочие (1 д. = 540): 7 д. → 7 раб. д. и т.п.; часы/минуты сверх целых
    ///    суток сохраняются (но не больше одного рабочего дня).
    ///
    /// Миграция написана вручную (без Designer-файла), снапшот модели обновлён.</summary>
    [DbContext(typeof(DelosferaDbContext))]
    [Migration("20260924170000_VndApprovalWorkingTimeCalendar")]
    public partial class VndApprovalWorkingTimeCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vnd_work_calendar_day",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vnd_work_calendar_day", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vnd_work_calendar_day_date",
                table: "vnd_work_calendar_day",
                column: "date",
                unique: true);

            migrationBuilder.AddColumn<bool>(
                name: "uses_working_time",
                table: "vnd_approval_process",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "primary_deadline_at",
                table: "vnd_approval_process",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "repeat_deadline_at",
                table: "vnd_approval_process",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "final_hold_deadline_at",
                table: "vnd_approval_process",
                type: "timestamp with time zone",
                nullable: true);

            // Существующие процессы: ровно та же формула, что раньше считалась на лету.
            migrationBuilder.Sql(@"
UPDATE vnd_approval_process SET
    primary_deadline_at    = primary_started_at + make_interval(mins => primary_deadline_minutes),
    repeat_deadline_at     = CASE WHEN repeat_started_at IS NULL THEN NULL
                                  ELSE repeat_started_at + make_interval(mins => repeat_deadline_minutes) END,
    final_hold_deadline_at = CASE WHEN final_hold_started_at IS NULL THEN NULL
                                  ELSE final_hold_started_at + make_interval(mins => final_hold_deadline_minutes) END;
");

            // Нормативы по умолчанию: календарные сутки → рабочие дни.
            migrationBuilder.Sql(@"
UPDATE vnd_approval_norm_settings SET
    primary_deadline_minutes    = (primary_deadline_minutes / 1440) * 540 + LEAST(primary_deadline_minutes % 1440, 540),
    repeat_deadline_minutes     = (repeat_deadline_minutes / 1440) * 540 + LEAST(repeat_deadline_minutes % 1440, 540),
    final_hold_deadline_minutes = (final_hold_deadline_minutes / 1440) * 540 + LEAST(final_hold_deadline_minutes % 1440, 540);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE vnd_approval_norm_settings SET
    primary_deadline_minutes    = (primary_deadline_minutes / 540) * 1440 + primary_deadline_minutes % 540,
    repeat_deadline_minutes     = (repeat_deadline_minutes / 540) * 1440 + repeat_deadline_minutes % 540,
    final_hold_deadline_minutes = (final_hold_deadline_minutes / 540) * 1440 + final_hold_deadline_minutes % 540;
");

            migrationBuilder.DropColumn(name: "final_hold_deadline_at", table: "vnd_approval_process");
            migrationBuilder.DropColumn(name: "repeat_deadline_at", table: "vnd_approval_process");
            migrationBuilder.DropColumn(name: "primary_deadline_at", table: "vnd_approval_process");
            migrationBuilder.DropColumn(name: "uses_working_time", table: "vnd_approval_process");

            migrationBuilder.DropTable(name: "vnd_work_calendar_day");
        }
    }
}
