namespace delosfera_server.Modules.Sz.DTO;

/// <summary>
/// Пресет полей записки в шаблоне (СЗ-6). Подмножество SzSaveRequest — только то, что
/// имеет смысл переносить между записками. Всё необязательно: шаблон может задавать
/// хоть один вид, хоть весь набор согласующих.
/// </summary>
public class SzTemplatePayload
{
    public int? KindId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public int? CorrespondentUnitId { get; set; }
    public int? AddresseeUserId { get; set; }
    public int? SignerUserId { get; set; }
    public bool? ApprovalIsParallel { get; set; }
    public bool? IsPaperCarrier { get; set; }
    public List<int> RubricIds { get; set; } = [];
    public List<int> ApproverUserIds { get; set; } = [];
    public List<int> ProposedAssigneeUserIds { get; set; } = [];
}

/// <summary>Создание/изменение шаблона записки.</summary>
public class SzTemplateSaveRequest
{
    public required string Name { get; set; }
    public SzTemplatePayload Payload { get; set; } = new();
}

/// <summary>Шаблон записки в списке и при применении.</summary>
public class SzTemplateResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public SzTemplatePayload Payload { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}
