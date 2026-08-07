namespace delosfera_server.Modules.Documents.VND.Models;

public enum ApprovalStageDecision
{
    Pending = 0, // в ожидании
    Approved = 1, // согласовано
    ApprovedWithComment = 2, // отправлено на устранение замечаний
    Rejected = 3, // отклонено
    AutoApprovedByTimeout = 4 // просрочка - засчитано как согласование
}