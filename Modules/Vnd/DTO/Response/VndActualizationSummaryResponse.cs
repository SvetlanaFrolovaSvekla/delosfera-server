namespace delosfera_server.Modules.Vnd.DTO.Response;

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
}