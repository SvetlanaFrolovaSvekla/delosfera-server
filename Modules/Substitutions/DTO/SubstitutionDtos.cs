using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.DTO;

/// <summary>Член комиссии приёма-передачи в запросе/ответе.</summary>
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
}

public class SubstitutionPage
{
    public List<SubstitutionListItem> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
