namespace delosfera_server.Modules.Substitutions.Models;

/// <summary>Состояние этапа согласования заявки на замещение.</summary>
public enum SubstitutionApprovalState
{
    Pending = 0,   // Ждёт своей очереди
    Active = 1,    // Текущий этап — на нём заявка ждёт решения
    Approved = 2,  // Согласовано
    Rejected = 3,  // Отклонено (заявка возвращается инициатору)
}

/// <summary>
/// Этап маршрута согласования заявки на замещение.
///
/// Заявка не является документом системного контура (нет DocumentId), поэтому маршрут
/// ведётся собственными этапами, а не общим движком: автор → директор филиала →
/// Операционное управление → УЧР → исполнение в УЧР. Порядок задаётся Order.
/// </summary>
public class SubstitutionApproval
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public SubstitutionRequest? Request { get; set; }

    /// <summary>Порядок этапа в маршруте (1, 2, 3, …).</summary>
    public int Order { get; set; }

    /// <summary>Роль этапа для показа в карточке: «Директор филиала», «Операционное управление», «УЧР».</summary>
    public required string RoleLabel { get; set; }

    /// <summary>Назначенный согласующий.</summary>
    public int UserId { get; set; }
    public delosfera_server.Modules.Users.Models.User? User { get; set; }

    public SubstitutionApprovalState State { get; set; } = SubstitutionApprovalState.Pending;

    /// <summary>Когда этап стал текущим (Active). От этого момента считается SLA согласования (ЗМ-SLA).</summary>
    public DateTime? ActivatedAt { get; set; }

    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
}
