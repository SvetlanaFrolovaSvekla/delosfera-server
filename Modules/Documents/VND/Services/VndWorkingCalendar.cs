using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Рабочее время для сроков согласования редакций ВНД: пн–пт в рабочие часы банка (по умолчанию
/// 09:00–18:00 по Бишкеку, задаются в справочнике "Производственный календарь" — см.
/// <see cref="VndWorkHoursSettings"/>) без праздников из того же справочника
/// (<see cref="VndWorkCalendarDay"/>).
///
/// Норматив хранится в РАБОЧИХ минутах: 1 д. = 1 рабочий день = длина рабочего дня банка
/// (по умолчанию 9 ч = 540 мин, <see cref="Rules.WorkDayMinutes"/>). Норматив «1 д.», выданный в
/// пятницу в 15:00, истекает в понедельник в 15:00; выданный в субботу — течёт с понедельника.
///
/// Класс чистый (без БД и без "сейчас") — правила передаются снимком <see cref="Rules"/>,
/// поэтому легко покрывается тестами и не нагружает систему: расчёт идёт по дням, а не по
/// минутам, — максимум ~130 итераций на норматив в 90 рабочих дней.
/// </summary>
public static class VndWorkingCalendar
{
    /// <summary>Рабочее время по умолчанию (пока справочник не заполнен): 09:00–18:00.</summary>
    public const int DefaultWorkStartMinutes = 9 * 60;
    public const int DefaultWorkEndMinutes = 18 * 60;

    /// <summary>Длина рабочего дня по умолчанию — «1 д.» в нормативах (9 ч).</summary>
    public const int DefaultWorkDayMinutes = DefaultWorkEndMinutes - DefaultWorkStartMinutes; // 540

    /// <summary>Верхняя граница норматива — 90 рабочих дней.</summary>
    public const int MaxDeadlineWorkDays = 90;

    public static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>Снимок календаря: рабочие часы банка и праздники. Суббота/воскресенье — всегда выходные.</summary>
    public sealed class Rules
    {
        public static readonly Rules Empty = new(new HashSet<DateOnly>());

        private readonly IReadOnlySet<DateOnly> _holidays;
        private readonly TimeSpan _start;
        private readonly TimeSpan _end;

        public Rules(IReadOnlySet<DateOnly> holidays,
            int workStartMinutes = DefaultWorkStartMinutes, int workEndMinutes = DefaultWorkEndMinutes)
        {
            if (workStartMinutes < 0 || workEndMinutes > 24 * 60 || workEndMinutes <= workStartMinutes)
                (workStartMinutes, workEndMinutes) = (DefaultWorkStartMinutes, DefaultWorkEndMinutes);

            _holidays = holidays;
            WorkStartMinutes = workStartMinutes;
            WorkEndMinutes = workEndMinutes;
            _start = TimeSpan.FromMinutes(workStartMinutes);
            _end = TimeSpan.FromMinutes(workEndMinutes);
        }

        public int WorkStartMinutes { get; }
        public int WorkEndMinutes { get; }

        /// <summary>Длина рабочего дня — «1 д.» в нормативах.</summary>
        public int WorkDayMinutes => WorkEndMinutes - WorkStartMinutes;

        public int MaxDeadlineMinutes => MaxDeadlineWorkDays * WorkDayMinutes;

        public int HolidayCount => _holidays.Count;

        public bool IsHoliday(DateOnly day) => _holidays.Contains(day);

        /// <summary>Рабочее окно дня в локальном времени банка или null, если день нерабочий.</summary>
        public (TimeSpan Start, TimeSpan End)? WindowOf(DateOnly day)
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return null;
            if (_holidays.Contains(day)) return null;
            return (_start, _end);
        }
    }

    /// <summary>Защита от зацикливания: 90 рабочих дней не могут растянуться дольше ~5 лет.</summary>
    private const int MaxDaysScanned = 2000;

    /// <summary>Прибавить к моменту (UTC) заданное число рабочих минут; результат — UTC.</summary>
    public static DateTime AddWorkingMinutes(DateTime fromUtc, int minutes, Rules rules)
    {
        if (minutes <= 0) return fromUtc;

        var local = ToLocal(fromUtc);
        var remaining = (double)minutes;

        for (var i = 0; i < MaxDaysScanned; i++)
        {
            var day = DateOnly.FromDateTime(local);
            var window = rules.WindowOf(day);
            var dayStart = local.Date;

            if (window is { } w && local.TimeOfDay < w.End)
            {
                var cursor = local.TimeOfDay < w.Start ? dayStart + w.Start : local;
                var available = (dayStart + w.End - cursor).TotalMinutes;

                if (remaining <= available)
                    return ToUtc(cursor.AddMinutes(remaining));

                remaining -= available;
            }

            local = dayStart.AddDays(1);
        }

        // Недостижимо при разумном календаре (кто-то объявил праздником всё подряд) —
        // лучше календарный срок, чем бесконечный.
        return fromUtc.AddMinutes(minutes);
    }

    /// <summary>
    /// Сколько рабочих минут между двумя моментами (UTC). Если <paramref name="toUtc"/> раньше
    /// <paramref name="fromUtc"/> — результат отрицательный (удобно для "просрочено на").
    /// </summary>
    public static int WorkingMinutesBetween(DateTime fromUtc, DateTime toUtc, Rules rules)
    {
        if (toUtc == fromUtc) return 0;
        if (toUtc < fromUtc) return -WorkingMinutesBetween(toUtc, fromUtc, rules);

        var from = ToLocal(fromUtc);
        var to = ToLocal(toUtc);
        var total = 0.0;

        var day = from.Date;
        for (var i = 0; i < MaxDaysScanned * 5 && day <= to; i++, day = day.AddDays(1))
        {
            if (rules.WindowOf(DateOnly.FromDateTime(day)) is not { } w) continue;

            var start = Max(day + w.Start, from);
            var end = Min(day + w.End, to);
            if (end > start) total += (end - start).TotalMinutes;
        }

        return (int)Math.Floor(total);
    }

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone),
            DateTimeKind.Utc);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bishkek"); }
        catch (TimeZoneNotFoundException)
        {
            // Кыргызстан живёт на UTC+6 круглый год, перевода часов нет.
            return TimeZoneInfo.CreateCustomTimeZone("KG", TimeSpan.FromHours(6), "Kyrgyzstan", "Kyrgyzstan");
        }
    }
}
