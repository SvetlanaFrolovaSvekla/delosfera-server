namespace delosfera_server.Modules.Vnd.Models;

public enum RedactionApprovalStatus
{
    NotRequired = 0, 
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Draft = 4 // Черновик, статус "Требуется согласование" - ещё не отправлена на согласование
}