namespace delosfera_server.Common.Services;

/// <summary>
/// Рабочий календарь банка (COND-1): сроки этапов маршрута отсчитываются в рабочих часах,
/// а не астрономических. Прежде срок считался как UtcNow.AddHours(норма) — норматив «8 часов»,
/// выданный в пятницу вечером, истекал в субботу ночью, когда работать некому. Здесь норма
/// тратится только внутри рабочих окон (пн–пт, 09:00–18:00 по времени банка), выходные
/// пропускаются.
///
/// Праздничный производственный календарь пока не учитывается — только выходные; праздники —
/// отдельная настройка (следующий шаг COND-1). Часовой пояс — как у BankClock (Asia/Bishkek).
/// </summary>
public static class WorkingCalendar
{
    private const int WorkStartHour = 9;
    private const int WorkEndHour = 18;   // 9 рабочих часов в дне

    private const string TimeZoneId = "Asia/Bishkek";
    private static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>Сегодняшняя календарная дата по времени банка (для печатных штампов/имён файлов).</summary>
    public static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

    /// <summary>Прибавить к моменту (UTC) заданное число рабочих часов и вернуть срок в UTC.</summary>
    public static DateTime AddWorkingHours(DateTime fromUtc, int hours)
    {
        if (hours <= 0) return fromUtc;

        var cursor = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), Zone);
        var remaining = TimeSpan.FromHours(hours);

        // Верхняя граница цикла — страховка от зацикливания; hours ограничен нормативами этапов.
        var guard = 0;
        while (remaining > TimeSpan.Zero && guard++ < 100_000)
        {
            cursor = MoveToWorkingTime(cursor);
            var dayEnd = cursor.Date.AddHours(WorkEndHour);
            var availableToday = dayEnd - cursor;

            if (remaining <= availableToday)
            {
                cursor = cursor.Add(remaining);
                remaining = TimeSpan.Zero;
            }
            else
            {
                remaining -= availableToday;
                cursor = dayEnd;   // следующая итерация перекатит на утро следующего рабочего дня
            }
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified), Zone);
    }

    /// <summary>Ближайший рабочий момент не раньше <paramref name="local"/> (локальное время банка).</summary>
    private static DateTime MoveToWorkingTime(DateTime local)
    {
        while (true)
        {
            if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                local = local.Date.AddDays(1).AddHours(WorkStartHour);
                continue;
            }
            if (local.TimeOfDay < TimeSpan.FromHours(WorkStartHour))
            {
                local = local.Date.AddHours(WorkStartHour);
                continue;
            }
            if (local.TimeOfDay >= TimeSpan.FromHours(WorkEndHour))
            {
                local = local.Date.AddDays(1).AddHours(WorkStartHour);
                continue;
            }
            return local;
        }
    }

    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("KG", TimeSpan.FromHours(6), "Kyrgyzstan", "Kyrgyzstan");
        }
    }
}
