namespace delosfera_server.Modules.Documents.VND.Models;

public enum ApprovalProcessStatus
{
    Primary = 0,          // первичное согласование в процессе
    RevisionNeeded = 1,   // на доработке - есть замечания/отклонения
    Repeated = 2,         // повторное согласование (только по замечаниям)
    FinalHold = 3,        // финальная выдержка - ознакомление без решений
    Approved = 4,         // завершено, ВНД стал действующим
    Cancelled = 5,        // отозван инициатором (на будущее, пока логики нет)
    Rejected = 6          // Отклонен
}