using delosfera_server.Common.Export;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Analytics.DTO.Request;

namespace delosfera_server.Modules.Analytics.Services;

public interface IVndAnalyticsExportService
{
    /// <summary>Аналитические срезы по ВНД одной книгой Excel (RPT-03).</summary>
    Task<byte[]> ExportAsync(AnalyticsPeriodRequest? period, string language);
}

/// <summary>
/// Выгрузка аналитики по ВНД (RPT-03).
///
/// Срезы разложены по листам, а не сведены в одну простыню: их читают разные люди —
/// методолог смотрит сроки согласования, руководитель СП свою нагрузку, а на Правление
/// уходит сводка. Одна страница на всех означала бы, что каждый ищет свои три строки.
/// </summary>
public class VndAnalyticsExportService : IVndAnalyticsExportService
{
    private readonly IVndAnalyticsService _analytics;
    private readonly IBankClock _clock;

    public VndAnalyticsExportService(IVndAnalyticsService analytics, IBankClock clock)
    {
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<byte[]> ExportAsync(AnalyticsPeriodRequest? period, string language)
    {
        var overview = await _analytics.GetOverviewAsync();
        var byStatus = await _analytics.GetStatusDistributionAsync(language);
        var byType = await _analytics.GetTypeDistributionAsync(language);
        var byDeveloper = await _analytics.GetDeveloperDistributionAsync(language, top: 50);
        var performance = await _analytics.GetApprovalPerformanceAsync(period);
        var workload = await _analytics.GetApproverWorkloadAsync(byUser: true);
        var dynamics = period is null ? [] : await _analytics.GetDynamicsAsync(period);

        var summary = new XlsxSheet
        {
            Name = "Сводка",
            Header = ["Показатель", "Значение"],
            Widths = [56, 16],
            Rows =
            [
                ["Дата выгрузки", _clock.Today.ToString("dd.MM.yyyy")],
                ["Всего ВНД", overview.Total.ToString()],
                ["Действующие", overview.Active.ToString()],
                ["На актуализации", overview.OnActualization.ToString()],
                ["На согласовании", overview.OnReview.ToString()],
                ["На консолидации", overview.OnConsolidation.ToString()],
                ["Черновики", overview.Draft.ToString()],
                ["В архиве", overview.Archived.ToString()],
                ["Требуют внимания", overview.RequiresAttention.ToString()],
                ["Просрочено", overview.Overdue.ToString()],
                ["Согласований в работе", overview.ApprovalsInProgress.ToString()],
                ["Создано за 30 дней", overview.CreatedLast30Days.ToString()],
                ["Утверждено за 30 дней", overview.PublishedLast30Days.ToString()],
                ["Средний срок согласования, дней", Number(overview.AverageApprovalDurationDays)],
                ["Доля решений по истечении срока, %", Number(overview.TimeoutDecisionRatePercent)],
            ],
        };

        var sheets = new List<XlsxSheet>
        {
            summary,
            Distribution("По статусам", "Статус", byStatus),
            Distribution("По видам ВНД", "Вид ВНД", byType),
            Distribution("По разработчикам", "Подразделение-разработчик", byDeveloper),
            new()
            {
                Name = "Нагрузка согласующих",
                Header =
                [
                    "Подразделение", "Согласующий", "Этапов всего", "Решено в срок",
                    "Автоакцепт по сроку", "С замечаниями/отклонено", "В работе",
                    "Средний срок решения, часов", "Доля автоакцепта, %",
                ],
                Widths = [40, 34, 14, 15, 20, 24, 12, 26, 22],
                Rows = workload
                    .Select(w => new[]
                    {
                        w.OrgUnitLabel,
                        w.ApproverLabel ?? "—",
                        w.TotalStages.ToString(),
                        w.DecidedOnTime.ToString(),
                        w.AutoApprovedByTimeout.ToString(),
                        w.WithCommentsOrRejected.ToString(),
                        w.Pending.ToString(),
                        Number(w.AverageDecisionHours),
                        Number(w.TimeoutRatePercent),
                    })
                    .ToList(),
            },
        };

        if (performance is not null)
        {
            sheets.Add(new XlsxSheet
            {
                Name = "Сроки согласования",
                Header = ["Показатель", "Значение"],
                Widths = [56, 16],
                Rows =
                [
                    ["Всего процессов согласования", performance.TotalProcesses.ToString()],
                    ["Согласовано", performance.Approved.ToString()],
                    ["Отклонено", performance.Rejected.ToString()],
                    ["Прервано", performance.Cancelled.ToString()],
                    ["В работе", performance.InProgress.ToString()],
                    ["Доля согласованных, %", Number(performance.ApprovalRatePercent)],
                    ["Доля возвратов на доработку, %", Number(performance.RevisionRatePercent)],
                    ["Средняя длительность, дней", Number(performance.AverageDurationDays)],
                    ["Медианная длительность, дней", Number(performance.MedianDurationDays)],
                ],
            });
        }

        if (dynamics.Count > 0)
        {
            sheets.Add(new XlsxSheet
            {
                Name = "Динамика",
                Header = ["Период", "Создано", "На согласование", "Утверждено", "В архив"],
                Widths = [18, 14, 20, 14, 12],
                Rows = dynamics
                    .Select(d => new[]
                    {
                        d.PeriodLabel,
                        d.Created.ToString(),
                        d.SentToApproval.ToString(),
                        d.Published.ToString(),
                        d.Archived.ToString(),
                    })
                    .ToList(),
            });
        }

        return XlsxWorkbook.Build(sheets.ToArray());
    }

    private static XlsxSheet Distribution(string sheetName, string keyTitle, List<DTO.Response.ChartCategoryPoint> points) => new()
    {
        Name = sheetName,
        Header = [keyTitle, "Количество", "Доля, %"],
        Widths = [50, 14, 12],
        Rows = points
            .Select(p => new[] {p.Label, p.Value.ToString(), Number(p.Percent)})
            .ToList(),
    };

    /// <summary>
    /// Числа пишем с запятой: файл открывают в русской локали Excel, и точка
    /// превратилась бы в текст, который не суммируется.
    /// </summary>
    private static string Number(double value) => value.ToString("0.##").Replace('.', ',');
}
