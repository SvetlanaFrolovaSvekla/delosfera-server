namespace delosfera_server.Modules.Vnd.DTO.Response;

/// <summary>Дефолтный согласующий для одного из фиксированных этапов маршрута</summary>
public class CoordinationDefaultApproverResponse
{
    public int Id { get; set; }

    /// <summary>"legal" | "risk_management" | "compliance" | "methodology"</summary>
    public required string Kind { get; set; }

    /// <summary>Человекочитаемое название этапа для отображения в справочнике</summary>
    public required string KindTitle { get; set; }

    /// <summary>Подразделение, к которому обязательно должен относиться согласующий этого этапа</summary>
    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}