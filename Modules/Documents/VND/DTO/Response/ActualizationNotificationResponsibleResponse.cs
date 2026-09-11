namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>
/// Один ответственный сотрудник за уведомления по актуализации ВНД подразделения OrgUnitId.
/// Сотрудник необязательно СОСТОИТ в этом подразделении — можно назначить ответственным и
/// человека из другого СП (см. комментарий у ActualizationNotificationService.SetResponsiblesAsync);
/// UserOrgUnitId/UserOrgUnitName — то СП, где сотрудник действительно числится, для фронта
/// (различает "свой"/"чужой" сотрудник в списке назначения).
/// </summary>
public class ActualizationNotificationResponsibleResponse
{
    public int Id { get; set; }

    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public int UserId { get; set; }
    public required string UserFullName { get; set; }

    public int? UserOrgUnitId { get; set; }
    public string? UserOrgUnitName { get; set; }
}
