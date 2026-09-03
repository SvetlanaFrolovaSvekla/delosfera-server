namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Данные для обновления записи справочника обязательных этапов: название, СП
/// и согласующий по умолчанию. ApproverUserId = null - сбросить дефолт (этап останется без
/// автоподстановки, потребуется выбрать согласующего вручную при запуске маршрута).</summary>
public class UpdateCoordinationDefaultApproverRequest
{
    public required string Title { get; set; }
    public required int OrgUnitId { get; set; }
    public int? ApproverUserId { get; set; }
}
