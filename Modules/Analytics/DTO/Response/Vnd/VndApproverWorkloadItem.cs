namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

/// <summary>Загрузка и скорость реакции согласующего подразделения/пользователя — для поиска
/// "узких мест" в маршруте согласования</summary>
public class VndApproverWorkloadItem
{
    /// <summary>Id согласующего подразделения</summary>
    public int OrgUnitId { get; set; }

    /// <summary>Название подразделения</summary>
    public required string OrgUnitLabel { get; set; }

    /// <summary>Id пользователя-согласующего (если рассматривается на уровне конкретного согласующего)</summary>
    public int? ApproverUserId { get; set; }

    /// <summary>ФИО согласующего (если применимо)</summary>
    public string? ApproverLabel { get; set; }

    /// <summary>Всего этапов согласования (первичных решений), в которых участвовал</summary>
    public int TotalStages { get; set; }

    /// <summary>Решений принято вовремя (не по таймауту)</summary>
    public int DecidedOnTime { get; set; }

    /// <summary>Решений зачтено автоматически по истечении срока (просрочка)</summary>
    public int AutoApprovedByTimeout { get; set; }

    /// <summary>Решений с замечаниями/отклонением</summary>
    public int WithCommentsOrRejected { get; set; }

    /// <summary>Ещё ожидают решения на данный момент</summary>
    public int Pending { get; set; }

    /// <summary>Среднее время принятия решения в часах (для решений, принятых не по таймауту)</summary>
    public double AverageDecisionHours { get; set; }

    /// <summary>Доля решений, зачтённых по таймауту, % — чем выше, тем больше подразделение
    /// "тормозит" процесс согласования</summary>
    public double TimeoutRatePercent { get; set; }
}
