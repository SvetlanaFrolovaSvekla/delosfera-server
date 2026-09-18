namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Главный редактор добавляет согласующего в уже запущенный процесс согласования —
/// см. VndApprovalService.AddApproverAsync.</summary>
public class AddApprovalStageRequest
{
    // id пользователя, который должен согласовывать на новом этапе
    public required int ApproverUserId { get; set; }
}

/// <summary>Главный редактор убирает согласующего из уже запущенного процесса согласования —
/// см. VndApprovalService.RemoveApproverAsync.</summary>
public class RemoveApprovalStageRequest
{
    /// <summary>Необязательная причина — попадает в журнал активности рядом с самим фактом
    /// удаления, отдельно от резолюции согласующего (её и не было — задача снята, а не решена).</summary>
    public string? Reason { get; set; }
}
