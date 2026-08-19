namespace delosfera_server.Modules.Analytics.DTO;

/// <summary>KPI-плитка рабочего стола.</summary>
public class DashboardKpiDto
{
    /// <summary>Код для навигации по клику: tasks, sz-inbox, prc-approval, overdue.</summary>
    public required string Code { get; set; }

    public required string Label { get; set; }
    public int Value { get; set; }

    /// <summary>Подпись под числом: «из них 2 просрочены».</summary>
    public string? Note { get; set; }

    /// <summary>Тон плитки: normal | warning | danger — по срочности, а не по контуру.</summary>
    public required string Tone { get; set; }
}

/// <summary>Активное замещение (GEN-14) для плашки на рабочем столе.</summary>
public class ActiveSubstitutionDto
{
    public int Id { get; set; }

    /// <summary>Кого замещает текущий пользователь.</summary>
    public int UserId { get; set; }
    public required string UserName { get; set; }

    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Сводка рабочего стола по всем контурам сразу.</summary>
public class DashboardSummaryDto
{
    public List<DashboardKpiDto> Kpis { get; set; } = [];

    /// <summary>Замещения, которые сейчас исполняет текущий пользователь.</summary>
    public List<ActiveSubstitutionDto> ActingFor { get; set; } = [];

    /// <summary>Замещения, оформленные на время отсутствия самого пользователя.</summary>
    public List<ActiveSubstitutionDto> ReplacedBy { get; set; } = [];
}
