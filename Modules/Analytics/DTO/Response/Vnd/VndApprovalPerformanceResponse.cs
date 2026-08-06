namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

using delosfera_server.Modules.Analytics.DTO.Response;

/// <summary>Показатели эффективности процесса согласования ВНД: сроки и итоги по завершённым процессам</summary>
public class VndApprovalPerformanceResponse
{
    /// <summary>Всего процессов согласования, попавших в выборку (по дате старта, если задан период)</summary>
    public int TotalProcesses { get; set; }

    /// <summary>Завершено успешно (ВНД стал действующим)</summary>
    public int Approved { get; set; }

    /// <summary>Отклонено</summary>
    public int Rejected { get; set; }

    /// <summary>Отозвано инициатором</summary>
    public int Cancelled { get; set; }

    /// <summary>Ещё идёт (Primary/Repeated/RevisionNeeded/FinalHold)</summary>
    public int InProgress { get; set; }

    /// <summary>Доля успешных от всех завершённых, % (Approved / (Approved+Rejected+Cancelled))</summary>
    public double ApprovalRatePercent { get; set; }

    /// <summary>Доля процессов, потребовавших повторного согласования (были замечания), %</summary>
    public double RevisionRatePercent { get; set; }

    /// <summary>Средняя длительность завершённого процесса в днях (от PrimaryStartedAt до CompletedAt)</summary>
    public double AverageDurationDays { get; set; }

    /// <summary>Медианная длительность завершённого процесса в днях</summary>
    public double MedianDurationDays { get; set; }

    /// <summary>Точки графика "среднее время согласования по периодам"</summary>
    public List<ChartTimePoint> DurationTrend { get; set; } = [];
}
