using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.DTO.Response;

/// <summary>Запись истории изменений подразделения (GEN-08).</summary>
public class OrgUnitHistoryResponse
{
    public int Id { get; set; }
    public OrgUnitChangeKind Kind { get; set; }
    public string KindTitle { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime At { get; set; }
}

/// <summary>Состояние подразделения на запрошенную дату.</summary>
public class OrgUnitSnapshotResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ParentTitle { get; set; }
    public string? HeadName { get; set; }
    public string? CuratorName { get; set; }

    /// <summary>Подразделение заведено позже запрошенной даты — на неё его ещё не существовало.</summary>
    public bool CreatedLater { get; set; }
}
