namespace delosfera_server.Modules.Analytics.Common;

/// <summary>Шаг группировки временных рядов на графиках аналитики</summary>
public enum AnalyticsGranularity
{
    /// <summary>По дням</summary>
    Day = 0,

    /// <summary>По неделям (начало недели - понедельник)</summary>
    Week = 1,

    /// <summary>По месяцам</summary>
    Month = 2,

    /// <summary>По кварталам</summary>
    Quarter = 3,

    /// <summary>По годам</summary>
    Year = 4
}
