namespace delosfera_server.Modules.Documents.VND.Models;

public enum ApprovalStageDecision
{
    Pending = 0, // в ожидании
    Approved = 1, // согласовано
    ApprovedWithComment = 2, // отправлено на устранение замечаний
    Rejected = 3, // отклонено
    AutoApprovedByTimeout = 4, // просрочка - засчитано как согласование

    /// <summary>Согласующий убран главным редактором из уже запущенного процесса согласования
    /// (см. VndApprovalService.RemoveApproverAsync) — задача с него снята, дальше он считается
    /// решённым (не Pending) на всех проверках "все решили", но не как "согласовано" и не как
    /// "отклонено": нейтральное, техническое значение, которое не влияет ни на переход на
    /// доработку (ApprovedWithComment/Rejected), ни на прекращение процесса (Rejected). Хранится
    /// как обычный int-столбец — новое значение не требует миграции.</summary>
    RemovedByEditor = 5
}