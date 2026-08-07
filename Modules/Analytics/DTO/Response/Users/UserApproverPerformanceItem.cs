namespace delosfera_server.Modules.Analytics.DTO.Response.Users;

/// <summary>Персональная статистика пользователя как согласующего по ВНД - рейтинг
/// самых быстрых/самых медленных согласующих</summary>
public class UserApproverPerformanceItem
{
    /// <summary>Id пользователя</summary>
    public int UserId { get; set; }

    /// <summary>ФИО</summary>
    public required string FullName { get; set; }

    /// <summary>Подразделение пользователя</summary>
    public string? OrgUnitLabel { get; set; }

    /// <summary>Всего решений принято (первичных)</summary>
    public int TotalDecisions { get; set; }

    /// <summary>Из них по таймауту (не принял решение вовремя)</summary>
    public int TimeoutDecisions { get; set; }

    /// <summary>Среднее время принятия решения в часах (без учёта решений по таймауту)</summary>
    public double AverageDecisionHours { get; set; }
}
