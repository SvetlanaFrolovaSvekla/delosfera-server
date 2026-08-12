namespace delosfera_server.Modules.Analytics.Common;

/// <summary>
/// Общая логика разбиения периода на бакеты (день/неделя/месяц/квартал/год) для
/// графиков-таймлайнов аналитики. Используется UserAnalyticsService и VndAnalyticsService -
/// один и тот же алгоритм группировки точек по времени для обоих модулей.
/// </summary>
public static class AnalyticsPeriodBucketing
{
    /// <summary>Дефолтное начало периода, если DateFrom не задан в запросе -
    /// «глубина» окна подобрана так, чтобы получалось около 12 точек на графике</summary>
    public static DateOnly DefaultFrom(DateOnly today, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => today.AddDays(-29),
        AnalyticsGranularity.Week => today.AddDays(-7 * 11),
        AnalyticsGranularity.Month => today.AddMonths(-11),
        AnalyticsGranularity.Quarter => today.AddMonths(-3 * 7),
        AnalyticsGranularity.Year => today.AddYears(-4),
        _ => today.AddMonths(-11)
    };

    /// <summary>Начало бакета, в который попадает дата, при заданном шаге группировки</summary>
    public static DateOnly BucketStart(DateOnly date, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => date,
        AnalyticsGranularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        AnalyticsGranularity.Month => new DateOnly(date.Year, date.Month, 1),
        AnalyticsGranularity.Quarter => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        AnalyticsGranularity.Year => new DateOnly(date.Year, 1, 1),
        _ => new DateOnly(date.Year, date.Month, 1)
    };

    /// <summary>
    /// Готовая подпись бакета для оси X графика и колонки отчёта.
    ///
    /// Культура задана явно: у сервиса она инвариантная, и месяцы выходили как
    /// «January 2026» — в отчёте, который уходит Правлению, это выглядит небрежно.
    /// </summary>
    public static string BucketLabel(DateOnly bucketStart, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => bucketStart.ToString("dd.MM.yyyy", Ru),
        AnalyticsGranularity.Week => $"{bucketStart.ToString("dd.MM", Ru)} — {bucketStart.AddDays(6).ToString("dd.MM.yyyy", Ru)}",
        AnalyticsGranularity.Month => Capitalize(bucketStart.ToString("MMMM yyyy", Ru)),
        AnalyticsGranularity.Quarter => $"{((bucketStart.Month - 1) / 3) + 1} кв. {bucketStart.Year}",
        AnalyticsGranularity.Year => bucketStart.Year.ToString(),
        _ => Capitalize(bucketStart.ToString("MMMM yyyy", Ru))
    };

    private static readonly System.Globalization.CultureInfo Ru = new("ru-RU");

    /// <summary>«август 2026» → «Август 2026»: подпись стоит в начале ячейки.</summary>
    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpper(value[0], Ru) + value[1..];

    /// <summary>Генерирует непрерывный список периодов от from до to с заданным шагом,
    /// чтобы на графике не было "дыр" там, где данных не было</summary>
    public static List<(DateOnly Start, string Label)> GeneratePeriods(
        DateOnly from, DateOnly to, AnalyticsGranularity granularity)
    {
        var result = new List<(DateOnly, string)>();
        var cursor = BucketStart(from, granularity);
        var end = BucketStart(to, granularity);
        var guard = 0;

        while (cursor <= end && guard < 500)
        {
            result.Add((cursor, BucketLabel(cursor, granularity)));

            cursor = granularity switch
            {
                AnalyticsGranularity.Day => cursor.AddDays(1),
                AnalyticsGranularity.Week => cursor.AddDays(7),
                AnalyticsGranularity.Month => cursor.AddMonths(1),
                AnalyticsGranularity.Quarter => cursor.AddMonths(3),
                AnalyticsGranularity.Year => cursor.AddYears(1),
                _ => cursor.AddMonths(1)
            };
            guard++;
        }

        return result;
    }
}