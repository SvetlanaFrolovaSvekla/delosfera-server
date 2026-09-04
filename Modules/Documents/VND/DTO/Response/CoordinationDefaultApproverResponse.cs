namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Одна запись справочника обязательных (фиксированных) этапов маршрута согласования</summary>
public class CoordinationDefaultApproverResponse
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>Порядковый номер этапа в маршруте (1,2,3...)</summary>
    public int Order { get; set; }

    /// <summary>Подразделение, к которому обязательно должен относиться согласующий этого этапа</summary>
    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
