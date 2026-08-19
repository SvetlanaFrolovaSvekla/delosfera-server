using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Поручение по служебной записке: резолюция руководителя раскладывается на конкретных
/// исполнителей со своими сроками. Записка исполнена, когда закрыты все её поручения.
/// </summary>
public class SzAssignment : IAuditableEntity
{
    public int Id { get; set; }

    public int SzDocumentId { get; set; }
    public SzDocument? SzDocument { get; set; }

    /// <summary>Исполнитель.</summary>
    public int AssigneeUserId { get; set; }
    public User? AssigneeUser { get; set; }

    /// <summary>СП исполнителя — по нему идёт контроль исполнительской дисциплины.</summary>
    public int? AssigneeUnitId { get; set; }
    public OrganizationUnit? AssigneeUnit { get; set; }

    /// <summary>Что поручено.</summary>
    public required string Text { get; set; }

    /// <summary>
    /// Ответственный исполнитель: он сводит результат, остальные — соисполнители.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>Срок поручения; по умолчанию — срок исполнения записки.</summary>
    public DateOnly? DueDate { get; set; }

    public SzAssignmentState State { get; set; } = SzAssignmentState.Open;

    /// <summary>Отчёт исполнителя — обязателен при сдаче поручения.</summary>
    public string? ReportText { get; set; }
    public DateTime? ReportedAt { get; set; }

    /// <summary>Кто принял или отклонил отчёт (автор резолюции либо автор записки).</summary>
    public int? ClosedByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>Причина возврата отчёта на доработку — исполнителю нужно знать, что не так.</summary>
    public string? ReturnReason { get; set; }

    /// <summary>Кто выдал поручение.</summary>
    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Состояние поручения по записке.</summary>
public enum SzAssignmentState
{
    /// <summary>Выдано, исполнитель работает.</summary>
    Open = 0,

    /// <summary>Исполнитель сдал отчёт, ждёт приёмки.</summary>
    Reported = 1,

    /// <summary>Отчёт принят.</summary>
    Done = 2,

    /// <summary>Поручение снято.</summary>
    Cancelled = 3
}
