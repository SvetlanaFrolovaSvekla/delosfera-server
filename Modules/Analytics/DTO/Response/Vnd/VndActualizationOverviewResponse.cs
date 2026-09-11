namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

using delosfera_server.Modules.Analytics.DTO.Response;

/// <summary>Сводные показатели по актуализации ВНД для вкладки "Актуализация" раздела Аналитика:
/// сроки по бакетам индикации, открытые циклы, их длительность и заявки на доступ к актуализации</summary>
public class VndActualizationOverviewResponse
{
    /// <summary>Действующих ВНД со сроком актуализации — именно по ним считается индикация ниже
    /// (черновики, документы на согласовании/консолидации/архиве в бакеты не попадают)</summary>
    public int TrackedTotal { get; set; }

    /// <summary>В норме — дальше порога "приближается"</summary>
    public int Normal { get; set; }

    /// <summary>Срок приближается</summary>
    public int Approaching { get; set; }

    /// <summary>Критично близко к сроку</summary>
    public int Critical { get; set; }

    /// <summary>Просрочено</summary>
    public int Overdue { get; set; }

    /// <summary>Циклов актуализации сейчас открыто (запущены, но ещё не опубликованы)</summary>
    public int OpenCycles { get; set; }

    /// <summary>Средняя длительность завершённого цикла в днях (от старта до публикации),
    /// по всем завершённым циклам за всё время</summary>
    public double AverageCycleDurationDays { get; set; }

    /// <summary>Медианная длительность завершённого цикла в днях</summary>
    public double MedianCycleDurationDays { get; set; }

    /// <summary>Доля завершённых циклов, в которых были реальные изменения документа (0-100%)</summary>
    public double CyclesWithChangesRatePercent { get; set; }

    /// <summary>Доля завершённых циклов, потребовавших согласования (0-100%)</summary>
    public double CyclesRequiringApprovalRatePercent { get; set; }

    /// <summary>Заявок на актуализацию, ожидающих решения главного редактора</summary>
    public int PendingRequests { get; set; }

    /// <summary>Заявок, одобренных за всё время</summary>
    public int ApprovedRequests { get; set; }

    /// <summary>Заявок, отклонённых за всё время</summary>
    public int RejectedRequests { get; set; }

    /// <summary>Топ подразделений-разработчиков по числу критичных и просроченных ВНД</summary>
    public List<ChartCategoryPoint> TopOverdueDevelopers { get; set; } = new();
}
