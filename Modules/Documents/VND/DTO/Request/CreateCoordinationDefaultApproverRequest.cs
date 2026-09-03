namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Данные для добавления нового обязательного (фиксированного) этапа в справочник.
/// Новый этап добавляется последним в маршруте (Order = максимальный текущий + 1) - порядок
/// можно изменить отдельно через reorder.</summary>
public class CreateCoordinationDefaultApproverRequest
{
    public required string Title { get; set; }
    public required int OrgUnitId { get; set; }

    /// <summary>Согласующий по умолчанию - необязателен, можно назначить позже</summary>
    public int? ApproverUserId { get; set; }
}
