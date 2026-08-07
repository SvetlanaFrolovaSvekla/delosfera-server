namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

/// <summary>Точка графика динамики циклов актуализации ВНД за период</summary>
public class VndActualizationTrendPoint
{
    /// <summary>Начало периода</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Подпись периода для оси X</summary>
    public required string PeriodLabel { get; set; }

    /// <summary>Сколько циклов актуализации запущено в периоде</summary>
    public int Started { get; set; }

    /// <summary>Сколько циклов завершено (опубликовано) в периоде</summary>
    public int Published { get; set; }

    /// <summary>Из завершённых - сколько содержали реальные изменения (HadChanges == true)</summary>
    public int PublishedWithChanges { get; set; }

    /// <summary>Средняя длительность цикла в днях (от старта до публикации), для циклов,
    /// завершённых в этом периоде</summary>
    public double AverageDurationDays { get; set; }
}
