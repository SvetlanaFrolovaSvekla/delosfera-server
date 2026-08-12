namespace delosfera_server.Modules.Workflow.DTO;

/// <summary>Задача в сводном реестре: что от меня ждут и по какому документу.</summary>
public class InboxTaskDto
{
    public int TaskId { get; set; }
    public int ParticipantId { get; set; }

    public int DocumentId { get; set; }
    public string? RegNumber { get; set; }
    public required string DocumentTitle { get; set; }

    /// <summary>Контур документа: Sz, Procurement, Vnd — по нему строится ссылка.</summary>
    public required string DocumentType { get; set; }
    public required string DocumentTypeTitle { get; set; }

    /// <summary>Тип задачи маршрута: согласование, устранение замечаний.</summary>
    public required string TaskType { get; set; }

    /// <summary>Номер этапа и его назначение — понятно, на какой стадии документ.</summary>
    public int StepOrder { get; set; }
    public required string StepKind { get; set; }

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
