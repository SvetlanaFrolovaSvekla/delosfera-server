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
    // Обязательный этап согласования (юр. отдел, методологи и др.)
    public required ApprovalStageKind Kind { get; set; } 
    // id пользователя, который должен согласовывать на данном этапе
    public required int ApproverUserId { get; set; }
}