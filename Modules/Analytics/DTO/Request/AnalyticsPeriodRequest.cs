using delosfera_server.Modules.Analytics.Common;

namespace delosfera_server.Modules.Analytics.DTO.Request;

/// <summary>Общий фильтр периода и шага группировки для графиков-таймлайнов</summary>
public class AnalyticsPeriodRequest
{
    /// <summary>Начало периода (включительно). Если не задано — берётся минимальная дата в данных</summary>
    public DateOnly? DateFrom { get; set; }

    /// <summary>Конец периода (включительно). Если не задано — берётся текущая дата</summary>
    public DateOnly? DateTo { get; set; }

    /// <summary>Шаг группировки точек на графике</summary>
    public AnalyticsGranularity Granularity { get; set; } = AnalyticsGranularity.Month;
}
