namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Резолюция руководителя: текст + поручения исполнителям.</summary>
public class SzResolutionRequest
{
    public required string Text { get; set; }
    public List<SzAssignmentRequest> Assignments { get; set; } = [];
}

public class SzAssignmentRequest
{
    public int AssigneeUserId { get; set; }
    public required string Text { get; set; }

    /// <summary>Ответственный исполнитель сводит результат; остальные — соисполнители.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Срок поручения; если не задан — берётся срок исполнения записки.</summary>
    public DateOnly? DueDate { get; set; }
}

public class SzAssignmentResponse
{
    public int Id { get; set; }
    public int SzDocumentId { get; set; }
    public int AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeUnit { get; set; }
    public required string Text { get; set; }
    public bool IsPrimary { get; set; }
    public DateOnly? DueDate { get; set; }
    public required string State { get; set; }
    public string? ReportText { get; set; }
    public DateTime? ReportedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ReturnReason { get; set; }

    /// <summary>Просрочено: срок прошёл, а поручение ещё не принято.</summary>
    public bool IsOverdue { get; set; }
    public int? DaysLeft { get; set; }

    // Контекст записки — нужен в очереди «Мои поручения», где карточка ещё не открыта.
    public string? SzRegNumber { get; set; }
    public string? SzTitle { get; set; }
}

public class SzReportRequest
{
    public required string ReportText { get; set; }
}

public class SzReturnRequest
{
    public required string Reason { get; set; }
}

public class SzExtendDueDateRequest
{
    public DateOnly DueDate { get; set; }
    public required string Reason { get; set; }
}

public class SzCompleteRequest
{
    /// <summary>Итог исполнения: чем закрыта записка.</summary>
    public required string Summary { get; set; }
}
