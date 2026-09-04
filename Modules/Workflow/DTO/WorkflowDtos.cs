using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.DTO;

// --- Requests ---

public class CreateRouteTemplateRequest
{
    public DocumentType DocumentType { get; set; }
    public required string Name { get; set; }
    public bool IsGlobalRule { get; set; }
    public List<TemplateStepDto> Steps { get; set; } = [];
}

public class TemplateStepDto
{
    public int Order { get; set; }
    public StepMode Mode { get; set; }
    public StepKind Kind { get; set; }
    public bool IsFinalMethodology { get; set; }
    public int? TimeNormHours { get; set; }

    /// <summary>
    /// Чем закрывается этап: простой подписью, квалифицированной или ничем.
    /// Пусто — этап закрывается решением без подписи.
    /// </summary>
    public Signing.Models.SignatureLevel? RequiredSignatureLevel { get; set; }
    public List<TemplateParticipantDto> Participants { get; set; } = [];
}

public class TemplateParticipantDto
{
    public int? UserId { get; set; }
    public int? UnitId { get; set; }
    public string? RoleRef { get; set; }
    public bool Required { get; set; }
}

public class InstantiateRequest
{
    public int DocumentId { get; set; }
    public int TemplateId { get; set; }
}

public class ResolveRequest
{
    public ResolutionType Type { get; set; }
    public string? Comment { get; set; }
    public int? SignatureId { get; set; }
}

// --- Responses ---

public class RouteInstanceResponse
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public required string Status { get; set; }
    public int CurrentStepOrder { get; set; }
    public List<StepResponse> Steps { get; set; } = [];
}

public class StepResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public required string Mode { get; set; }
    public required string Kind { get; set; }
    public bool IsFinalMethodology { get; set; }

    /// <summary>
    /// Чем закрывается этап: Simple, Qualified или пусто — без подписи. Клиенту это
    /// нужно до нажатия кнопки: под квалифицированную подпись открывается рабочее
    /// место с криптопровайдером, а простая ставится самим нажатием.
    /// </summary>
    public string? RequiredSignatureLevel { get; set; }

    public List<ParticipantResponse> Participants { get; set; } = [];
}

public class ParticipantResponse
{
    public int Id { get; set; }
    public int? UserId { get; set; }

    /// <summary>
    /// ФИО участника: карточка документа показывает лист согласования всем участникам,
    /// а справочник пользователей доступен не каждой роли.
    /// </summary>
    public string? UserFullName { get; set; }

    public bool Required { get; set; }
    public required string State { get; set; }
    public ResolutionResponse? Resolution { get; set; }
}

public class ResolutionResponse
{
    public required string Type { get; set; }
    public string? Comment { get; set; }
    public List<RemarkResponse> Remarks { get; set; } = [];

    /// <summary>Подпись под резолюцией — то, что печатается штампом.</summary>
    public SignatureStampResponse? Signature { get; set; }
}

/// <summary>
/// Визуальный штамп подписи (SIG-03). Реквизиты берутся из самой подписи, а не из
/// справочника: должность подписанта может измениться, а штамп обязан остаться
/// таким, каким был в момент подписания.
/// </summary>
public class SignatureStampResponse
{
    public int Id { get; set; }
    public required string LevelTitle { get; set; }
    public string? FullName { get; set; }
    public string? Position { get; set; }
    public DateTime At { get; set; }

    /// <summary>Отпечаток подписанного — короткая часть для показа на штампе.</summary>
    public string? Fingerprint { get; set; }

    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>
    /// Время, удостоверённое службой меток. Отличается от At тем, что его назвал не
    /// наш сервер: именно оно доказывает, что подпись поставлена, пока сертификат
    /// действовал. Пусто — метки нет.
    /// </summary>
    public DateTime? TimestampedAt { get; set; }

    public string? TimestampAuthority { get; set; }

    /// <summary>Каким удостоверяющим центром подтверждён сертификат подписанта.</summary>
    public string? TrustAuthority { get; set; }

    /// <summary>
    /// Что в этой подписи осталось непроверенным: цепочка, отзыв, метка. Штамп обязан
    /// это показывать — иначе подпись выглядит доказательнее, чем она есть.
    /// </summary>
    public List<string> Caveats { get; set; } = [];
}

public class RemarkResponse
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public required string State { get; set; }
}

/// <summary>Шаблон маршрута с этапами — для экрана настройки уровня подписи.</summary>
public class RouteTemplateResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string DocumentType { get; set; }
    public bool IsGlobalRule { get; set; }
    public List<RouteTemplateStepResponse> Steps { get; set; } = [];
}

public class RouteTemplateStepResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public required string Mode { get; set; }
    public required string Kind { get; set; }
    public bool IsFinalMethodology { get; set; }
    public int? TimeNormHours { get; set; }
    public int ParticipantCount { get; set; }

    /// <summary>
    /// Кто согласует на этапе. Раньше отдавалось только их число — по нему видно,
    /// что участники есть, но не видно, кто именно, и настроить маршрут было
    /// нельзя: экран не знал, что показывать.
    /// </summary>
    public List<TemplateParticipantResponse> Participants { get; set; } = [];

    /// <summary>Null — подпись не требуется; Simple — ПЭП; Qualified — ЭЦП.</summary>
    public Signing.Models.SignatureLevel? RequiredSignatureLevel { get; set; }
}

/// <summary>
/// Участник этапа шаблона: конкретный человек, подразделение или роль.
///
/// Подразделение и роль разрешаются в человека в момент запуска маршрута — так
/// шаблон переживает смену людей в должностях.
/// </summary>
public class TemplateParticipantResponse
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public string? UserName { get; set; }

    public int? UnitId { get; set; }
    public string? UnitTitle { get; set; }

    /// <summary>Ролевая ссылка: «руководитель подразделения автора» и подобные.</summary>
    public string? RoleRef { get; set; }

    /// <summary>Обязателен: без его решения этап не закрывается.</summary>
    public bool Required { get; set; }
}

public class StepSignatureLevelRequest
{
    /// <summary>Пусто — снять требование подписи с этапа.</summary>
    public Signing.Models.SignatureLevel? Level { get; set; }
}
