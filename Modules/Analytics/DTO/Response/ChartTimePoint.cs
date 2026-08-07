namespace delosfera_server.Modules.Analytics.DTO.Response;

/// <summary>Универсальная точка временного ряда с одним значением (для линейных/столбчатых графиков динамики)</summary>
public class ChartTimePoint
{
    /// <summary>Начало периода (день/неделя/месяц/квартал/год - в зависимости от заданной группировки)</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Готовая подпись периода для оси X (например "Янв 2026", "2026-W05")</summary>
    public required string PeriodLabel { get; set; }

    /// <summary>Значение точки</summary>
    public int Value { get; set; }
}
