using delosfera_server.Modules.Meetings.Models;

namespace delosfera_server.Modules.Meetings.DTO;

// ── Заседание ────────────────────────────────────────────────────────────────

public class MeetingCreateRequest
{
    public MeetingBody Body { get; set; }

    /// <summary>Номер заседания. Не задан — берётся следующий свободный в году по этому органу.</summary>
    public int? Number { get; set; }

    public MeetingForm Form { get; set; } = MeetingForm.InPerson;

    /// <summary>Секретарь. Не задан — тот, кто создаёт запись.</summary>
    public int? SecretaryUserId { get; set; }
    public int? SecretaryUnitId { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    public string? MaterialsUrl { get; set; }
}

public class MeetingUpdateRequest
{
    public int? Number { get; set; }
    public MeetingForm? Form { get; set; }
    public int? SecretaryUserId { get; set; }
    public int? SecretaryUnitId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string? MaterialsUrl { get; set; }
}

public class MeetingListItemDto
{
    public int Id { get; set; }
    public MeetingBody Body { get; set; }
    public string BodyTitle { get; set; } = string.Empty;
    public int Number { get; set; }
    public int Year { get; set; }
    public MeetingForm Form { get; set; }
    public string FormTitle { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string? SecretaryName { get; set; }

    public int ItemCount { get; set; }
    public int AssignmentCount { get; set; }

    /// <summary>Поручения с истёкшим сроком и незакрытым статусом — то, ради чего открывают журнал.</summary>
    public int OverdueCount { get; set; }

    public DateTime? NotifiedAt { get; set; }
}

public class MeetingDto : MeetingListItemDto
{
    public int SecretaryUserId { get; set; }
    public int? SecretaryUnitId { get; set; }
    public string? SecretaryUnitTitle { get; set; }
    public string? MaterialsUrl { get; set; }

    public List<AgendaItemDto> Items { get; set; } = [];

    /// <summary>Может ли текущий пользователь править заседание и повестку.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Может ли текущий пользователь заполнять отчёты об исполнении.</summary>
    public bool CanReport { get; set; }
}

// ── Вопрос повестки ──────────────────────────────────────────────────────────

public class AgendaItemRequest
{
    public string Topic { get; set; } = string.Empty;
    public string? ProtocolNumber { get; set; }
    public DateOnly? ProtocolDate { get; set; }

    /// <summary>Проект постановления — готовится к заседанию.</summary>
    public string? DraftResolution { get; set; }

    /// <summary>Принятое решение — записывается после заседания.</summary>
    public string? Decision { get; set; }

    public int? SpeakerUserId { get; set; }
    public int? SpeakerHeadUserId { get; set; }
    public int? SpeakerUnitId { get; set; }
    public int? DeputySecretaryUserId { get; set; }
    public int? ControllerUserId { get; set; }

    public string? DocumentsUrl { get; set; }
}

public class AgendaItemDto
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public int Order { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string? ProtocolNumber { get; set; }
    public DateOnly? ProtocolDate { get; set; }
    public string? DraftResolution { get; set; }
    public string? Decision { get; set; }

    public int? SpeakerUserId { get; set; }
    public string? SpeakerName { get; set; }
    public int? SpeakerHeadUserId { get; set; }
    public string? SpeakerHeadName { get; set; }
    public int? SpeakerUnitId { get; set; }
    public string? SpeakerUnitTitle { get; set; }
    public int? DeputySecretaryUserId { get; set; }
    public string? DeputySecretaryName { get; set; }
    public int? ControllerUserId { get; set; }
    public string? ControllerName { get; set; }

    public string? DocumentsUrl { get; set; }

    public List<AgendaGuestDto> Guests { get; set; } = [];
    public List<AgendaAssignmentDto> Assignments { get; set; } = [];
    public List<AgendaFileDto> Files { get; set; } = [];
}

public class AgendaGuestRequest
{
    public int UserId { get; set; }
    public int? OrgUnitId { get; set; }
}

public class AgendaGuestDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? OrgUnitId { get; set; }
    public string? OrgUnitTitle { get; set; }
}

// ── Поручения ────────────────────────────────────────────────────────────────

public class AgendaAssignmentRequest
{
    public int UserId { get; set; }
    public int? OrgUnitId { get; set; }

    /// <summary>Что поручено. Пусто — поручение по решению целиком.</summary>
    public string? Text { get; set; }

    public DateOnly? DueDate { get; set; }
}

/// <summary>Отчёт исполнителя: единственное, что он меняет в вопросе повестки.</summary>
public class AgendaReportRequest
{
    public ExecutionStatus Status { get; set; }
    public string? Report { get; set; }
}

public class AgendaAssignmentDto
{
    public int Id { get; set; }
    public int AgendaItemId { get; set; }

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? OrgUnitId { get; set; }
    public string? OrgUnitTitle { get; set; }

    /// <summary>Что поручено; пусто — поручение по решению целиком.</summary>
    public string? Text { get; set; }

    public DateOnly? DueDate { get; set; }
    public ExecutionStatus Status { get; set; }
    public string StatusTitle { get; set; } = string.Empty;

    public string? Report { get; set; }
    public DateTime? ReportedAt { get; set; }
    public string? ReportedByName { get; set; }

    /// <summary>Срок истёк, а поручение не закрыто.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Дней до срока; отрицательное — просрочка.</summary>
    public int? DaysLeft { get; set; }
}

public class AgendaFileDto
{
    public int Id { get; set; }
    public MeetingFileKind Kind { get; set; }
    public string KindTitle { get; set; } = string.Empty;
    public int FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Файлы удаляет только секретарь — остальным кнопка не показывается.</summary>
    public bool CanDelete { get; set; }
}

// ── Уведомления и реестр ─────────────────────────────────────────────────────

public class MeetingNotifyResultDto
{
    public int MeetingId { get; set; }
    public int RecipientCount { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];
}

public class MeetingFilterRequest
{
    public MeetingBody? Body { get; set; }
    public int? Year { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Только заседания с просроченными поручениями.</summary>
    public bool OverdueOnly { get; set; }
}

/// <summary>
/// Куда документ ушёл на коллегиальный орган: заседание, номер вопроса, решение.
///
/// Связь в данных была всегда, а в карточке её не показывали — человек видел
/// документ и не знал, дошёл ли он до Правления и чем там кончилось.
/// </summary>
public class BoardReviewDto
{
    public int MeetingId { get; set; }
    public string BodyTitle { get; set; } = "";
    public DateOnly MeetingDate { get; set; }

    public int AgendaItemId { get; set; }

    /// <summary>Номер вопроса в повестке — по нему человек ищет себя в заседании.</summary>
    public int Order { get; set; }
    public string Topic { get; set; } = "";

    /// <summary>Проект постановления — готовится к заседанию.</summary>
    public string? DraftResolution { get; set; }

    /// <summary>Принятое решение; пусто — заседание ещё не прошло.</summary>
    public string? Decision { get; set; }

    public string? ProtocolNumber { get; set; }
    public DateOnly? ProtocolDate { get; set; }
}
