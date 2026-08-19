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
    public bool IsAccountant { get; set; }
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
    public DateOnly? SubmissionDeadline { get; set; }
    public DateOnly? OpenedOn { get; set; }

    public string? CommissionOrderNumber { get; set; }
    public DateOnly? CommissionOrderDate { get; set; }

    public int? PreviousTenderId { get; set; }
    public string? FailureReason { get; set; }

    public List<CommissionMemberDto> Commission { get; set; } = [];
    public List<TenderBidDto> Bids { get; set; } = [];

    /// <summary>Требования к составу комиссии для этой суммы (PRC-14).</summary>
    public int RequiredSize { get; set; }
    public int RequiredBoardMembers { get; set; }
    public bool RequiresAccountant { get; set; }
    public bool RequiresBoardChairman { get; set; }

    /// <summary>Кворум заседания — не менее двух третей состава (PRC-15).</summary>
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
