namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Главный редактор добавляет согласующего в уже запущенный процесс согласования —
/// см. VndApprovalService.AddApproverAsync.</summary>
public class AddApprovalStageRequest
{
    // id пользователя, который должен согласовывать на новом этапе
    public required int ApproverUserId { get; set; }
}

/// <summary>Главный редактор убирает согласующего из уже запущенного процесса согласования —
/// см. VndApprovalService.RemoveApproverAsync. Только для Custom-этапов (добавленных вручную) —
/// обязательные (Fixed и legacy Legal/RiskManagement/Compliance/Methodology) этапы этим способом
/// не убираются, см. ReplaceApprovalStageRequest ниже.</summary>
public class RemoveApprovalStageRequest
{
    /// <summary>Необязательная причина — попадает в журнал активности рядом с самим фактом
    /// удаления, отдельно от резолюции согласующего (её и не было — задача снята, а не решена).</summary>
    public string? Reason { get; set; }
}

/// <summary>Главный редактор заменяет согласующего на обязательном этапе маршрута, не убирая сам
/// этап — см. VndApprovalService.ReplaceApproverAsync.</summary>
public class ReplaceApprovalStageRequest
{
    // id пользователя, который станет согласующим на этом этапе вместо прежнего
    public required int NewApproverUserId { get; set; }

    /// <summary>Необязательная причина — попадает в журнал активности.</summary>
    public string? Reason { get; set; }
}
