using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Член комиссии в карточке конкурса.</summary>
public class CommissionMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string UserName { get; set; }
    public CommissionRole Role { get; set; }
    public required string RoleTitle { get; set; }
    public bool IsBoardMember { get; set; }

    /// <summary>Постоянные члены по п. 120: УБУиО, Юридическая служба, Управление безопасности.</summary>
    public bool IsAccountant { get; set; }
    public bool IsLegal { get; set; }
    public bool IsSecurity { get; set; }

    public bool AttendedOpening { get; set; }
    public string? DissentingOpinion { get; set; }

    /// <summary>Голосует ли: у секретаря и эксперта голоса нет.</summary>
    public bool IsVoting { get; set; }

    /// <summary>Заключение эксперта и приложенный к нему файл.</summary>
    public string? Conclusion { get; set; }
    public int? ConclusionAttachmentId { get; set; }
    public string? ConclusionFileName { get; set; }
    public DateTime? ConclusionAt { get; set; }
}

/// <summary>Конкурсная заявка в карточке.</summary>
public class TenderBidDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }
    public decimal Price { get; set; }
    public DateOnly SubmittedOn { get; set; }
    public bool IsLate { get; set; }
    public bool IsAdmitted { get; set; }
    public string? RejectionReason { get; set; }
    public int? Score { get; set; }
    public string? Specification { get; set; }
    public bool IsWinner { get; set; }
    public bool SupplierBlacklisted { get; set; }

    /// <summary>Как проголосовали по этой заявке (п. 24.2).</summary>
    public List<BidVoteDto> Votes { get; set; } = [];

    public int VotesFor { get; set; }
    public int VotesAgainst { get; set; }
    public int VotesAbstained { get; set; }

    /// <summary>
    /// Заявка прошла голосованием: за неё большинство голосовавших, либо при
    /// равенстве «за» подал председатель — его голос решающий (п. 24.3).
    /// </summary>
    public bool Carried { get; set; }

    /// <summary>Почему заявка не прошла — показывается рядом с итогом голосования.</summary>
    public string? VoteOutcomeNote { get; set; }
}

/// <summary>Голос одного члена комиссии по заявке.</summary>
public class BidVoteDto
{
    public int MemberId { get; set; }
    public required string MemberName { get; set; }
    public required string RoleTitle { get; set; }
    public bool IsChairman { get; set; }
    public VoteChoice Choice { get; set; }
    public required string ChoiceTitle { get; set; }
}

/// <summary>Карточка конкурса (PRC-13..16).</summary>
public class TenderDto
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public string? RegNumber { get; set; }

    public TenderStatus Status { get; set; }
    public required string StatusTitle { get; set; }

    public required string Subject { get; set; }
    public decimal Amount { get; set; }
    public bool IsLimited { get; set; }

    public DateOnly? PublishedOn { get; set; }

    /// <summary>Где объявление размещено; пусто — размещение не отмечено.</summary>
    public string? PublishedAt { get; set; }
    public string? PublicationConfirmedByName { get; set; }
    public DateTime? PublicationConfirmedAt { get; set; }
    public DateOnly? SubmissionDeadline { get; set; }
    public DateOnly? OpenedOn { get; set; }

    /// <summary>
    /// До какой даты комиссия обязана изучить заявки и оформить протокол:
    /// вскрытие плюс десять рабочих дней (п. 11.2/11.3 Положения).
    /// </summary>
    public DateOnly? StudyDeadline { get; set; }

    /// <summary>
    /// Сколько рабочих дней осталось на изучение. Отрицательное — срок прошёл:
    /// видно не только то, что просрочено, но и насколько.
    /// </summary>
    public int? StudyDaysLeft { get; set; }

    public string? CommissionOrderNumber { get; set; }
    public DateOnly? CommissionOrderDate { get; set; }

    public int? PreviousTenderId { get; set; }
    public string? FailureReason { get; set; }

    public List<CommissionMemberDto> Commission { get; set; } = [];
    public List<TenderBidDto> Bids { get; set; } = [];

    /// <summary>Назначенное заседание комиссии; переносы — в MeetingChanges.</summary>
    public DateOnly? MeetingDate { get; set; }
    public List<MeetingChangeDto> MeetingChanges { get; set; } = [];

    /// <summary>Состав комиссии по п. 120 Положения: пять членов с правом голоса.</summary>
    public int RequiredSize { get; set; }
    public int RequiredBoardMembers { get; set; }

    /// <summary>Куратор инициатора — он не может быть председателем комиссии (п. 120).</summary>
    public int? InitiatorCuratorUserId { get; set; }

    /// <summary>Кворум заседания — не менее двух третей состава (п. 24.1).</summary>
    public int QuorumRequired { get; set; }
    public int Attended { get; set; }
    public bool HasQuorum { get; set; }

    /// <summary>Чего не хватает, чтобы двигать конкурс дальше.</summary>
    public List<string> Blockers { get; set; } = [];
}

public class TenderCreateRequest
{
    public bool IsLimited { get; set; }
    public DateOnly? SubmissionDeadline { get; set; }
    public string? CommissionOrderNumber { get; set; }
    public DateOnly? CommissionOrderDate { get; set; }

    /// <summary>Повторный конкурс: состав комиссии копируется из предыдущего (PRC-16).</summary>
    public int? PreviousTenderId { get; set; }
}

public class CommissionMemberRequest
{
    public int UserId { get; set; }
    public CommissionRole Role { get; set; } = CommissionRole.Member;
    public bool IsBoardMember { get; set; }
    public bool IsAccountant { get; set; }
    public bool IsLegal { get; set; }
    public bool IsSecurity { get; set; }
}

/// <summary>Перенос заседания комиссии — или назначение его впервые.</summary>
public class MeetingChangeDto
{
    public DateOnly? FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public required string Reason { get; set; }
    public required string ByUserName { get; set; }
    public DateTime At { get; set; }
}

public class MeetingScheduleRequest
{
    public DateOnly Date { get; set; }

    /// <summary>Основание переноса. При первом назначении не требуется.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Внесение голосов по заявке по итогам очного заседания (п. 24.2).
///
/// Голоса приходят пачкой на одну заявку, а не по одному: заседание проходит
/// целиком, и вносить его результат частями значит оставлять протокол
/// недописанным между сохранениями.
/// </summary>
public class BidVotesRequest
{
    public List<MemberVote> Votes { get; set; } = [];

    public class MemberVote
    {
        public int MemberId { get; set; }
        public VoteChoice Choice { get; set; }
    }
}

public class TenderBidRequest
{
    public int? SupplierId { get; set; }
    public string? SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }
    public decimal Price { get; set; }
    public DateOnly? SubmittedOn { get; set; }
    public string? Specification { get; set; }
}

/// <summary>Оценка заявки комиссией.</summary>
public class BidScoreRequest
{
    public int Score { get; set; }
}

/// <summary>Отметка участия члена комиссии в заседании и его особое мнение.</summary>
public class AttendanceRequest
{
    public bool Attended { get; set; }
    public string? DissentingOpinion { get; set; }
}

public class TenderPublishRequest
{
    public DateOnly SubmissionDeadline { get; set; }
}

/// <summary>Отметка о фактическом размещении объявления.</summary>
public class PublicationConfirmRequest
{
    /// <summary>Где выложено: сайт Банка, tenders.kg, разосланные приглашения.</summary>
    public string? PublishedAt { get; set; }
}

public class TenderFailRequest
{
    public required string Reason { get; set; }
    public bool Cancel { get; set; }
}

/// <summary>Заключение эксперта комиссии: текст, файл или и то, и другое.</summary>
public class ExpertConclusionRequest
{
    public string? Conclusion { get; set; }

    /// <summary>Вложение документа закупки с файлом заключения.</summary>
    public int? AttachmentId { get; set; }
}
