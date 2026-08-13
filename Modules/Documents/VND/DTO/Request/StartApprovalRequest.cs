using System.ComponentModel.DataAnnotations;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class StartApprovalRequest
{
    // Список этапов согласования
    [Required, MinLength(3, ErrorMessage = "Маршрут должен содержать минимум 3 этапа")]
    public required List<ApprovalStageRequest> Stages { get; set; }

    // Норматив первичного согласования, в минутах
    [Range(1, 43200, ErrorMessage = "Норматив должен быть от 1 минуты до 30 дней")]
    public required int PrimaryDeadlineMinutes { get; set; }
    // Норматив согласования после исправленных замечаний, в минутах
    [Range(1, 43200)]
    public required int RepeatDeadlineMinutes { get; set; }
    // Норматив финальной выдержки, в минутах
    [Range(1, 43200)]
    public required int FinalHoldDeadlineMinutes { get; set; }
}

public class ApprovalStageRequest
{
    // Обязательный этап согласования (юр. отдел, методологи и др.)
    public required ApprovalStageKind Kind { get; set; } 
    // id пользователя, который должен согласовывать на данном этапе
    public required int ApproverUserId { get; set; }
}