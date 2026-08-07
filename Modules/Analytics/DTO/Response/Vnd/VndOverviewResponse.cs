namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

/// <summary>Сводные показатели-карточки по ВНД для верхней части страницы отчётности (KPI-плашки)</summary>
public class VndOverviewResponse
{
    /// <summary>Всего документов в системе (все статусы, включая черновики и архив)</summary>
    public int Total { get; set; }

    /// <summary>Действующие ВНД</summary>
    public int Active { get; set; }

    /// <summary>На актуализации</summary>
    public int OnActualization { get; set; }

    /// <summary>На согласовании</summary>
    public int OnReview { get; set; }

    /// <summary>На консолидации</summary>
    public int OnConsolidation { get; set; }

    /// <summary>Архивированные</summary>
    public int Archived { get; set; }

    /// <summary>Черновики</summary>
    public int Draft { get; set; }

    /// <summary>Документы, у которых наступил или прошёл срок актуализации (Critical + Overdue)</summary>
    public int RequiresAttention { get; set; }

    /// <summary>Просроченные по сроку актуализации</summary>
    public int Overdue { get; set; }

    /// <summary>Активных процессов согласования прямо сейчас (Primary/Repeated/FinalHold/RevisionNeeded)</summary>
    public int ApprovalsInProgress { get; set; }

    /// <summary>Новых ВНД за последние 30 дней</summary>
    public int CreatedLast30Days { get; set; }

    /// <summary>Редакций опубликовано за последние 30 дней (публикация актуализации)</summary>
    public int PublishedLast30Days { get; set; }

    /// <summary>Средняя длительность цикла согласования в днях (от старта первичного согласования
    /// до завершения), по завершённым процессам за всё время</summary>
    public double AverageApprovalDurationDays { get; set; }

    /// <summary>Доля решений согласующих, зачтённых автоматически по таймауту (0-100%) -
    /// индикатор дисциплины согласования</summary>
    public double TimeoutDecisionRatePercent { get; set; }
}
