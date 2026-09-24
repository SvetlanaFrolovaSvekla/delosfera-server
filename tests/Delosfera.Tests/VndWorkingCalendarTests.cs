using delosfera_server.Modules.Documents.VND.Services;
using Xunit;

namespace Delosfera.Tests;

/// <summary>Сроки согласования ВНД в рабочем времени: пн–пт в рабочие часы банка (по умолчанию
/// 09:00–18:00 по Бишкеку), праздники — из справочника.</summary>
public class VndWorkingCalendarTests
{
    private const int Day = VndWorkingCalendar.DefaultWorkDayMinutes; // 540

    private static DateTime Utc(int y, int m, int d, int hh, int mm) =>
        VndWorkingCalendar.ToUtc(new DateTime(y, m, d, hh, mm, 0));

    private static DateTime Local(DateTime utc) => VndWorkingCalendar.ToLocal(utc);

    private static VndWorkingCalendar.Rules Holidays(params (int y, int m, int d)[] days) =>
        new(days.Select(x => new DateOnly(x.y, x.m, x.d)).ToHashSet());

    [Fact]
    public void OneWorkingDay_FromFridayAfternoon_EndsMondaySameTime()
    {
        // Пт 25.09.2026 15:00 + 1 раб. день → Пн 28.09 15:00.
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 25, 15, 0), Day, VndWorkingCalendar.Rules.Empty);
        Assert.Equal(new DateTime(2026, 9, 28, 15, 0, 0), Local(due));
    }

    [Fact]
    public void StartedOnWeekend_CountsFromMondayNine()
    {
        // Сб 26.09 11:00 + 2 ч → Пн 28.09 11:00.
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 26, 11, 0), 120, VndWorkingCalendar.Rules.Empty);
        Assert.Equal(new DateTime(2026, 9, 28, 11, 0, 0), Local(due));
    }

    [Fact]
    public void AfterWorkEnd_CountsFromNextMorning()
    {
        // Пн 28.09 19:30 + 30 мин → Вт 29.09 09:30.
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 28, 19, 30), 30, VndWorkingCalendar.Rules.Empty);
        Assert.Equal(new DateTime(2026, 9, 29, 9, 30, 0), Local(due));
    }

    [Fact]
    public void FullDayFromMorning_EndsAtSixPmSameDay()
    {
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 28, 9, 0), Day, VndWorkingCalendar.Rules.Empty);
        Assert.Equal(new DateTime(2026, 9, 28, 18, 0, 0), Local(due));
    }

    [Fact]
    public void Holiday_IsSkipped()
    {
        // Пн 31.08.2026 — День независимости. Пт 28.08 15:00 + 1 день → Вт 01.09 15:00.
        var rules = Holidays((2026, 8, 31));
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 8, 28, 15, 0), Day, rules);
        Assert.Equal(new DateTime(2026, 9, 1, 15, 0, 0), Local(due));
    }

    [Fact]
    public void CustomBankHours_AreUsed()
    {
        // Рабочее время банка 08:30–17:00 (8,5 ч). Пн 16:00 + 2 ч → 1 ч в пн, 1 ч во вт → Вт 09:30.
        var rules = new VndWorkingCalendar.Rules(new HashSet<DateOnly>(), 8 * 60 + 30, 17 * 60);
        Assert.Equal(510, rules.WorkDayMinutes);
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 28, 16, 0), 120, rules);
        Assert.Equal(new DateTime(2026, 9, 29, 9, 30, 0), Local(due));
    }

    [Fact]
    public void HolidayOnWeekend_ChangesNothing()
    {
        var rules = Holidays((2026, 9, 26));
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 25, 15, 0), Day, rules);
        Assert.Equal(new DateTime(2026, 9, 28, 15, 0, 0), Local(due));
    }

    [Fact]
    public void SevenWorkingDays_SpanOneAndHalfWeeks()
    {
        // Чт 24.09 10:00 + 7 раб. дней → Пн 05.10 10:00 (выходные 26–27.09 и 03–04.10 пропущены).
        var due = VndWorkingCalendar.AddWorkingMinutes(Utc(2026, 9, 24, 10, 0), 7 * Day, VndWorkingCalendar.Rules.Empty);
        Assert.Equal(new DateTime(2026, 10, 5, 10, 0, 0), Local(due));
    }

    [Fact]
    public void Between_IsInverseOfAdd()
    {
        var rules = Holidays((2026, 8, 31), (2026, 9, 3));
        var start = Utc(2026, 8, 27, 13, 17);
        foreach (var minutes in new[] { 1, 59, 540, 541, 3 * 540 + 17, 20 * 540 })
        {
            var due = VndWorkingCalendar.AddWorkingMinutes(start, minutes, rules);
            Assert.Equal(minutes, VndWorkingCalendar.WorkingMinutesBetween(start, due, rules));
        }
    }

    [Fact]
    public void Between_IsNegativeWhenOverdue()
    {
        var deadline = Utc(2026, 9, 28, 10, 0);
        var now = Utc(2026, 9, 28, 12, 30);
        Assert.Equal(-150, VndWorkingCalendar.WorkingMinutesBetween(now, deadline, VndWorkingCalendar.Rules.Empty));
    }

    [Fact]
    public void Between_OverWeekend_IsZero()
    {
        Assert.Equal(0, VndWorkingCalendar.WorkingMinutesBetween(
            Utc(2026, 9, 25, 18, 0), Utc(2026, 9, 28, 9, 0), VndWorkingCalendar.Rules.Empty));
    }
}
