using delosfera_server.Common.Services;
using Xunit;

namespace Delosfera.Tests;

/// <summary>Рабочий календарь (COND-1): норма тратится только в рабочие окна, выходные пропускаются.</summary>
public class WorkingCalendarTests
{
    private static readonly TimeZoneInfo Bishkek = Resolve();

    private static TimeZoneInfo Resolve()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bishkek"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("KG", TimeSpan.FromHours(6), "Kyrgyzstan", "Kyrgyzstan");
        }
    }

    private static DateTime Utc(int y, int m, int d, int hh, int mm) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, m, d, hh, mm, 0, DateTimeKind.Unspecified), Bishkek);

    private static DateTime Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Bishkek);

    [Fact]
    public void WithinWorkingDay_AddsHours()
    {
        // Пн 05.01.2026 10:00 + 2 раб.ч = 12:00 того же дня.
        var due = WorkingCalendar.AddWorkingHours(Utc(2026, 1, 5, 10, 0), 2);
        Assert.Equal(new DateTime(2026, 1, 5, 12, 0, 0), Local(due));
    }

    [Fact]
    public void CrossesDayEnd_RollsToNextMorning()
    {
        // Пн 17:00 + 2 раб.ч: 1 ч до 18:00, остаток 1 ч → Вт 10:00.
        var due = WorkingCalendar.AddWorkingHours(Utc(2026, 1, 5, 17, 0), 2);
        Assert.Equal(new DateTime(2026, 1, 6, 10, 0, 0), Local(due));
    }

    [Fact]
    public void SkipsWeekend()
    {
        // Пт 09.01.2026 17:00 + 2 раб.ч → Пн 12.01.2026 10:00 (сб/вс пропущены).
        var due = WorkingCalendar.AddWorkingHours(Utc(2026, 1, 9, 17, 0), 2);
        Assert.Equal(new DateTime(2026, 1, 12, 10, 0, 0), Local(due));
    }

    [Fact]
    public void BeforeWorkStart_CountsFromNineAm()
    {
        // Пн 07:00 (до начала дня) + 1 раб.ч → отсчёт с 09:00 → 10:00.
        var due = WorkingCalendar.AddWorkingHours(Utc(2026, 1, 5, 7, 0), 1);
        Assert.Equal(new DateTime(2026, 1, 5, 10, 0, 0), Local(due));
    }

    [Fact]
    public void FullWorkingDayNorm_LandsNextDaySameStart()
    {
        // 9 рабочих часов = полный день: Пн 09:00 + 9 ч → Пн 18:00 (конец дня), не переносится на утро.
        var due = WorkingCalendar.AddWorkingHours(Utc(2026, 1, 5, 9, 0), 9);
        Assert.Equal(new DateTime(2026, 1, 5, 18, 0, 0), Local(due));
    }

    [Fact]
    public void ZeroOrNegative_ReturnsInput()
    {
        var from = Utc(2026, 1, 5, 10, 0);
        Assert.Equal(from, WorkingCalendar.AddWorkingHours(from, 0));
    }
}
