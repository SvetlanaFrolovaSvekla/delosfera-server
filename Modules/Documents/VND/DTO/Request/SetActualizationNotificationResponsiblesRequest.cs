namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>
/// Полная замена списка ответственных сотрудников для одного СП (не инкремент): пришедший
/// UserIds становится новым составом ответственных этого подразделения целиком — так же,
/// как MultiSelectField на фронте отдаёт выбранный набор целиком, а не дельту.
/// </summary>
public class SetActualizationNotificationResponsiblesRequest
{
    public required int OrgUnitId { get; set; }
    public List<int> UserIds { get; set; } = [];
}
