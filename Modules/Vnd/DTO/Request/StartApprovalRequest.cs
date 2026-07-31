using delosfera_server.Modules.Vnd.Models;

namespace delosfera_server.Modules.Vnd.DTO.Request;

// TODO: добавить для нормативов только рабочие часы - с 9 до 18 (?) или в общем
// Тогда что делать, если внд отправлена на согласование в пятницу?

public class StartApprovalRequest
{
    // Список этапов согласования
    public required List<ApprovalStageRequest> Stages { get; set; }

    // Норматив первичного согласования
    public required int PrimaryDeadlineHours { get; set; }
    // Норматив согласования после исправленных замечаний
    public required int RepeatDeadlineHours { get; set; }
    // Норматив финальной выдержки
    public required int FinalHoldDeadlineHours { get; set; }
}

public class ApprovalStageRequest
{
    // Обязательный этап согласования (юр. отдел, методологи и др.)
    public required ApprovalStageKind Kind { get; set; } 
    // id пользователя, который должен согласовывать на данном этапе
    public required int ApproverUserId { get; set; }
}