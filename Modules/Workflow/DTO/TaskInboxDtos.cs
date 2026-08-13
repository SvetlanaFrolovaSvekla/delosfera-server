namespace delosfera_server.Modules.Workflow.DTO;

/// <summary>Задача в сводном реестре: что от меня ждут и по какому документу.</summary>
public class InboxTaskDto
{
    public int TaskId { get; set; }

    /// <summary>
    /// Участник маршрута, если задача пришла из согласования. У задач контура
    /// (решение адресата, поручение) участника нет — они привязаны к документу.
    /// </summary>
    public int? ParticipantId { get; set; }

    public int DocumentId { get; set; }

    /// <summary>
    /// Идентификатор записи контура — записки, заявки. Карточки открываются именно
    /// по нему, а не по документу: ссылка по DocumentId ведёт на чужую карточку.
    /// </summary>
    public int? EntityId { get; set; }

    public string? RegNumber { get; set; }
    public required string DocumentTitle { get; set; }

    /// <summary>Контур документа: Sz, Procurement, Vnd — по нему строится ссылка.</summary>
    public required string DocumentType { get; set; }
    public required string DocumentTypeTitle { get; set; }

    /// <summary>Тип задачи маршрута: согласование, устранение замечаний.</summary>
    public required string TaskType { get; set; }

    /// <summary>
    /// Номер этапа и его назначение — понятно, на какой стадии документ.
    /// У задач вне маршрута этапа нет.
    /// </summary>
    public int? StepOrder { get; set; }
    public string? StepKind { get; set; }

    public DateTime? DueAt { get; set; }
    public bool IsOverdue { get; set; }

    /// <summary>
    /// Задача пришла по замещению: показываем, за кого пользователь её выполняет,
    /// чтобы решение не выглядело чужим (GEN-14).
    /// </summary>
    public string? OnBehalfOf { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Сводка по реестру задач.</summary>
public class TaskInboxDto
{
    public List<InboxTaskDto> Tasks { get; set; } = [];
    public int Total { get; set; }
    public int Overdue { get; set; }

    /// <summary>Сколько задач получено по замещению.</summary>
    public int Delegated { get; set; }
}
