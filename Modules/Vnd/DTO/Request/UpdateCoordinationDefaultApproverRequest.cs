namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Данные для обновления дефолтного согласующего одного из фиксированных этапов.
/// ApproverUserId = null - сбросить дефолт (этап останется без автоподстановки)</summary>
public class UpdateCoordinationDefaultApproverRequest
{
    public int? ApproverUserId { get; set; }
}