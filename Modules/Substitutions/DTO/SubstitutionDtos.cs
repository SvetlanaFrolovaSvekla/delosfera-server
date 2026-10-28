using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.DTO;

/// <summary>Член комиссии приёма-передачи в запросе/ответе.</summary>
public class ApprovalStepDto
{
    public int Id { get; set; }

    /// <summary>Круг согласования (1 — первая отправка, 2 — после отклонения…). Действующий — последний.</summary>
    public int Round { get; set; } = 1;

    public int Order { get; set; }
    public string RoleLabel { get; set; } = "";
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string State { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>Кто фактически вынес решение — отличается от UserId, если решал замещающий (GEN-14).</summary>
    public int? DecidedByUserId { get; set; }
}

public class CommissionMemberDto
{
    public int? UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
}

/// <summary>Создание/правка заявки на замещение (КСЗ-В9).</summary>
public class SubstitutionSaveRequest
{
    // Блок 1
    public int? InitiatorUserId { get; set; }
    public string Subject { get; set; } = "";
    public SubstitutionReason Reason { get; set; } = SubstitutionReason.Other;

    // Блок 2 — отсутствующий
    public int? AbsentUserId { get; set; }
    public string AbsentName { get; set; } = "";
    public string? AbsentPosition { get; set; }
    public string? AbsentBranch { get; set; }
    public int? AbsentUnitId { get; set; }

    // Блок 3 — замещающий
    public int? SubstituteUserId { get; set; }
    public string SubstituteName { get; set; } = "";
    public string? SubstitutePosition { get; set; }
    public string? SubstituteBranch { get; set; }
    public int? SubstituteUnitId { get; set; }
    public string? PassportSeriesNumber { get; set; }
    public string? PassportIssuedBy { get; set; }
    public string? Inn { get; set; }
    public DateOnly? PassportIssuedOn { get; set; }
    public DateOnly? PassportValidUntil { get; set; }
    public string? AddressRegistration { get; set; }
    public string? AddressResidence { get; set; }

    // Блок 4 — период
    public int? DaysCount { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }

    // Блок 5 — комиссия
    public int? CommissionChairUserId { get; set; }
    public string? CommissionChairName { get; set; }
    public string? CommissionChairPosition { get; set; }
    public HandoverMoment HandoverMoment { get; set; } = HandoverMoment.EndOfDay;
    public DateOnly? HandoverOn { get; set; }
    public List<CommissionMemberDto> CommissionMembers { get; set; } = [];

    // Блок 6
    public string? Description { get; set; }
}

/// <summary>Строка реестра заявок на замещение.</summary>
public class SubstitutionListItem
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public string Status { get; set; } = "";
    public string StatusTitle { get; set; } = "";
    public string Subject { get; set; } = "";
    public string ReasonTitle { get; set; } = "";
    public string AbsentName { get; set; } = "";
    public string SubstituteName { get; set; } = "";
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string? InitiatorName { get; set; }

    /// <summary>Автор заявки (кто создал) — неизменяемое поле.</summary>
    public int? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Карточка заявки на замещение.</summary>
public class SubstitutionDetails : SubstitutionListItem
{
    public string ReasonCode { get; set; } = "";
    public int InitiatorUserId { get; set; }

    public int? AbsentUserId { get; set; }
    public string? AbsentPosition { get; set; }
    public string? AbsentBranch { get; set; }
    public int? AbsentUnitId { get; set; }
    public string? AbsentUnit { get; set; }

    public int? SubstituteUserId { get; set; }
    public string? SubstitutePosition { get; set; }
    public string? SubstituteBranch { get; set; }
    public int? SubstituteUnitId { get; set; }
    public string? SubstituteUnit { get; set; }
    public string? PassportSeriesNumber { get; set; }
    public string? PassportIssuedBy { get; set; }
    public string? Inn { get; set; }
    public DateOnly? PassportIssuedOn { get; set; }
    public DateOnly? PassportValidUntil { get; set; }
    public string? AddressRegistration { get; set; }
    public string? AddressResidence { get; set; }

    public int? DaysCount { get; set; }

    public int? CommissionChairUserId { get; set; }
    public string? CommissionChairName { get; set; }
    public string? CommissionChairPosition { get; set; }
    public string HandoverMoment { get; set; } = "";
    public DateOnly? HandoverOn { get; set; }
    public List<CommissionMemberDto> CommissionMembers { get; set; } = [];

    public string? Description { get; set; }

    /// <summary>Предупреждение: паспорт истекает раньше окончания замещения (КСЗ-20).</summary>
    public bool PassportExpiresBeforeEnd { get; set; }

    /// <summary>
    /// Маршрут согласования: директор филиала → Опер. управление → УЧР — все круги по
    /// порядку (Round, Order); действующий маршрут — последний круг.
    /// </summary>
    public List<ApprovalStepDto> Approvals { get; set; } = [];

    /// <summary>За кого текущий пользователь действует по замещению (GEN-14) — его этапы он тоже согласует.</summary>
    public List<int> ActingForUserIds { get; set; } = [];

    /// <summary>Как фактически решавших зовут: id → ФИО (для «согласовал Б за А»).</summary>
    public Dictionary<int, string> DeciderNames { get; set; } = new();

    /// <summary>Какие этапы не сформировались при отправке и почему — пометка для карточки.</summary>
    public string? RouteNote { get; set; }

    /// <summary>Права текущего пользователя на эту заявку (заполняет контроллер).</summary>
    public SubstitutionAccessDto? Access { get; set; }

    /// <summary>Дополнительные подписанты договора МО от имени Банка (добавлены в заявке).</summary>
    public List<int> ExtraBankSignerUserIds { get; set; } = [];

    /// <summary>Кто подписывает договор МО от имени Банка: основной (Председатель Правления из
    /// настроек маршрута УЧР) и дополнительные — по порядку.</summary>
    public List<BankSignerDto> BankSigners { get; set; } = [];
}

/// <summary>Подписант договора МО от имени Банка.</summary>
public class BankSignerDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Position { get; set; }

    /// <summary>Основной подписант из настроек маршрута УЧР (Председатель Правления).</summary>
    public bool IsMain { get; set; }
}

/// <summary>Дополнительные подписанты договора МО от имени Банка — список целиком.</summary>
public class SubstitutionBankSignersRequest
{
    public List<int> UserIds { get; set; } = [];
}

/// <summary>Что текущему пользователю можно делать с заявкой.</summary>
public class SubstitutionAccessDto
{
    /// <summary>Ведёт заявки как УЧР (кадровик из настроек маршрута или администратор).</summary>
    public bool CanManage { get; set; }
    public bool IsInitiator { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanDelete { get; set; }
    public bool CanExecute { get; set; }

    /// <summary>Корректировать заявку на согласовании/подписании (право CorrectSubstitution или УЧР).</summary>
    public bool CanCorrect { get; set; }

    /// <summary>Добавлять/убирать дополнительных подписантов договора МО от Банка: делопроизводство
    /// (право CorrectSubstitution) и УЧР — пока заявка не исполнена.</summary>
    public bool CanEditBankSigners { get; set; }
}

public class SubstitutionPage
{
    public List<SubstitutionListItem> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
