using delosfera_server.Modules.Correspondence.Models;

namespace delosfera_server.Modules.Correspondence.DTO;

public class LetterSaveRequest
{
    public LetterDirection Direction { get; set; } = LetterDirection.Incoming;
    public LetterCategory Category { get; set; } = LetterCategory.Ordinary;

    /// <summary>Дата регистрации. Пусто — сегодня. Письма заводят и задним числом.</summary>
    public DateOnly? RegisteredOn { get; set; }

    public int CorrespondentId { get; set; }
    public string? TheirNumber { get; set; }
    public DateOnly? TheirDate { get; set; }

    public string Subject { get; set; } = "";
    public string? Summary { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Post;
    public int? SheetCount { get; set; }
    public string? Enclosures { get; set; }

    public int? ResponsibleUserId { get; set; }
    public int? ResponsibleUnitId { get; set; }

    /// <summary>Срок ответа. Пусто — берётся норматив категории.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Ставить ли на контроль. Пусто — по умолчанию для категории.</summary>
    public bool? IsControlled { get; set; }

    /// <summary>Входящее, на которое отвечает это исходящее.</summary>
    public int? InReplyToId { get; set; }

    /// <summary>Служебная записка, которой инициировано исходящее.</summary>
    public int? SourceSzId { get; set; }

    public int? NomenclatureCaseId { get; set; }

    /// <summary>Сохранить исходящее проектом — номер присвоится при отправке.</summary>
    public bool AsDraft { get; set; }
}

public class ResolveLetterRequest
{
    public string Resolution { get; set; } = "";
    public int? ResponsibleUserId { get; set; }
    public int? ResponsibleUnitId { get; set; }
    public DateOnly? DueDate { get; set; }
}

public class CloseLetterRequest
{
    public string? Note { get; set; }
}

public class LetterFilterRequest
{
    public LetterDirection? Direction { get; set; }
    public List<LetterCategory>? Categories { get; set; }
    public List<LetterStatus>? Statuses { get; set; }
    public int? CorrespondentId { get; set; }
    public int? ResponsibleUserId { get; set; }
    public int? UnitId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? OnlyControlled { get; set; }
    public bool? OnlyOverdue { get; set; }
    public string? Text { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class LetterDto
{
    public int Id { get; set; }
    public string Direction { get; set; } = "";
    public string Category { get; set; } = "";
    public string? RegNumber { get; set; }
    public DateOnly? RegisteredOn { get; set; }

    public int CorrespondentId { get; set; }
    public string? CorrespondentTitle { get; set; }
    public string? CorrespondentKind { get; set; }

    public string? TheirNumber { get; set; }
    public DateOnly? TheirDate { get; set; }

    public string Subject { get; set; } = "";
    public string? Summary { get; set; }
    public string DeliveryMethod { get; set; } = "";
    public int? SheetCount { get; set; }
    public string? Enclosures { get; set; }

    public string Status { get; set; } = "";

    public string? Resolution { get; set; }
    public DateTime? ResolutionAt { get; set; }
    public string? ResolutionBy { get; set; }

    public int? ResponsibleUserId { get; set; }
    public string? ResponsibleName { get; set; }
    public string? ResponsibleUnit { get; set; }

    public DateOnly? DueDate { get; set; }
    public bool IsControlled { get; set; }
    public bool IsOverdue { get; set; }

    /// <summary>Дней до срока. Отрицательное — просрочено на столько.</summary>
    public int? DaysLeft { get; set; }

    public string? ExecutionNote { get; set; }
    public DateTime? ExecutedAt { get; set; }

    public int? InReplyToId { get; set; }
    public string? InReplyToNumber { get; set; }
    public int ReplyCount { get; set; }
    public int? SourceSzId { get; set; }

    public int FileCount { get; set; }
}

public class LetterListResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<LetterDto> Items { get; set; } = [];
}

public class CorrespondentSaveRequest
{
    public string Title { get; set; } = "";
    public string? ShortTitle { get; set; }
    public CorrespondentKind Kind { get; set; } = CorrespondentKind.Other;
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}
