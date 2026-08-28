using delosfera_server.Modules.Obligations.Models;

namespace delosfera_server.Modules.Obligations.Services;

/// <summary>
/// Разбивка на периоды.
///
/// Периоды календарные, а не «каждые 30 дней от даты заведения»: регулятор
/// спрашивает про январь и первый квартал, а не про промежуток с 14 января по
/// 13 февраля. Обязательство, заведённое в середине месяца, попадает в текущий
/// период целиком — иначе первый месяц выпал бы из контроля.
/// </summary>
public static class ObligationCalendar
{
    /// <summary>Период, которому принадлежит день.</summary>
    public static (DateOnly Start, DateOnly End) PeriodOf(DateOnly day, Periodicity periodicity)
    {
        switch (periodicity)
        {
            case Periodicity.Weekly:
            {
                // Неделя с понедельника: рабочая неделя банка, а не воскресная.
                var shift = ((int)day.DayOfWeek + 6) % 7;
                var start = day.AddDays(-shift);
                return (start, start.AddDays(6));
            }

            case Periodicity.Monthly:
            {
                var start = new DateOnly(day.Year, day.Month, 1);
                return (start, start.AddMonths(1).AddDays(-1));
            }

            case Periodicity.Quarterly:
            {
                var quarter = (day.Month - 1) / 3;
                var start = new DateOnly(day.Year, quarter * 3 + 1, 1);
                return (start, start.AddMonths(3).AddDays(-1));
            }

            case Periodicity.SemiAnnual:
            {
                var half = day.Month <= 6 ? 1 : 7;
                var start = new DateOnly(day.Year, half, 1);
                return (start, start.AddMonths(6).AddDays(-1));
            }

            case Periodicity.Annual:
            {
                var start = new DateOnly(day.Year, 1, 1);
                return (start, start.AddYears(1).AddDays(-1));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(periodicity), periodicity, "Неизвестная периодичность.");
        }
    }

    /// <summary>Начало следующего периода.</summary>
    public static DateOnly NextStart(DateOnly periodEnd) => periodEnd.AddDays(1);

    /// <summary>
    /// Периоды обязательства от начала действия до указанного дня включительно.
    /// Ограничение сверху обязательно: бессрочное обязательство иначе породило бы
    /// бесконечный список.
    /// </summary>
    public static IEnumerable<(DateOnly Start, DateOnly End, DateOnly Due)> Periods(
        RecurringObligation obligation, DateOnly upTo)
    {
        var limit = obligation.EndsOn is {} ends && ends < upTo ? ends : upTo;
        var (start, end) = PeriodOf(obligation.StartsOn, obligation.Periodicity);

        while (start <= limit)
        {
            yield return (start, end, end.AddDays(obligation.GraceDays));

            start = NextStart(end);
            (_, end) = PeriodOf(start, obligation.Periodicity);
        }
    }
}
