using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class StartApprovalRequest
{
    // Список этапов согласования
    public required List<ApprovalStageRequest> Stages { get; set; }

    // Норматив первичного согласования, в минутах
    public required int PrimaryDeadlineMinutes { get; set; }
    // Норматив согласования после исправленных замечаний, в минутах
    public required int RepeatDeadlineMinutes { get; set; }
    // Норматив финальной выдержки, в минутах
    public required int FinalHoldDeadlineMinutes { get; set; }
}

public class ApprovalStageRequest
{
    /// <summary>Id записи справочника обязательных этапов (dictionaries/coordination-users),
    /// если этот этап - один из обязательных фиксированных. null - произвольный (Custom) этап,
    /// добавленный инициатором вручную. Ведущие этапы маршрута (в начале списка Stages) должны
    /// 1-в-1, в том же порядке, соответствовать активным записям справочника - см.
    /// VndApprovalService.BuildAndValidateStagesAsync.</summary>
    public int? CoordinationStageId { get; set; }
    // id пользователя, который должен согласовывать на данном этапе
    public required int ApproverUserId { get; set; }
}