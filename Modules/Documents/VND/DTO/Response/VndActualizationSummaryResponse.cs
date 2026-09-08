namespace delosfera_server.Modules.Documents.VND.DTO.Response;

// Для метрик по актуализации
public class VndActualizationSummaryResponse
{
    public int Normal { get; set; }
    public int Approaching { get; set; }
    public int Critical { get; set; }
    public int Overdue { get; set; }

    /// <summary>Normal + Approaching + Critical + Overdue.
    /// Документы без DueActualizationDate сюда не входят.</summary>
    public int Total { get; set; }

    /// <summary>Всего ВНД на странице "Планирование актуализации" (тот же набор статусов, что и
    /// сам список документов - см. ACTUALIZATION_PLANNING_STATUSES на фронте) - в отличие от Total
    /// выше, не требует наличия DueActualizationDate, поэтому может быть больше Total.</summary>
    public int TotalActive { get; set; }

    /// <summary>Из них — ни разу не актуализированные, т.е. с единственной (первой) редакцией.
    /// Тот же критерий, что у чекбокса "Только ни разу не актуализированные" в фильтрах.</summary>
    public int NeverActualized { get; set; }
}