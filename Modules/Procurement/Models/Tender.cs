using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>Стадия конкурса (PRC-13..16).</summary>
public enum TenderStatus
{
    /// <summary>Готовится конкурсная документация.</summary>
    Draft = 1,

    /// <summary>Объявление опубликовано, идёт приём заявок.</summary>
    Published = 2,

    /// <summary>Срок приёма истёк, заявки вскрыты.</summary>
    Opened = 3,

    /// <summary>Комиссия оценила заявки и определила победителя.</summary>
    Decided = 4,

    /// <summary>Конкурс не состоялся — недостаточно допущенных заявок.</summary>
    Failed = 5,

    /// <summary>Конкурс отменён с указанием основания.</summary>
    Cancelled = 6,
}

/// <summary>Роль в комиссии по закупке (PRC-14).</summary>
public enum CommissionRole
{
    Chairman = 1,
    Member = 2,

    /// <summary>Секретарь — ведёт протоколы, в кворум не входит.</summary>
    Secretary = 3,

    /// <summary>
    /// Эксперт — привлекается по отдельным закупкам за специальными знаниями:
    /// даёт письменное заключение по предмету, но не голосует и в кворум не входит.
    /// </summary>
    Expert = 4,
}

/// <summary>
/// Конкурс по закупке (PRC-13..16): документация, публикация, конкурсный период,
/// вскрытие заявок и решение комиссии.
///
/// Конкурс — отдельная сущность, а не поля заявки: у него своя комиссия, свои сроки
/// и свои протоколы, а по одной заявке конкурс может проводиться повторно (PRC-16).
/// </summary>
public class Tender : IAuditableEntity
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    public string? RegNumber { get; set; }

    public TenderStatus Status { get; set; } = TenderStatus.Draft;

    /// <summary>Конкурс с ограниченным участием — объявление не публикуется (PRC-13).</summary>
    public bool IsLimited { get; set; }

    /// <summary>Дата публикации объявления на сайте Банка и tenders.kg.</summary>
    public DateOnly? PublishedOn { get; set; }

    /// <summary>
    /// Окончательный срок приёма заявок. Конкурсный период — не менее 5 рабочих дней
    /// с публикации; заявки после срока к вскрытию не принимаются (PRC-15).
    /// </summary>
    public DateOnly? SubmissionDeadline { get; set; }

    public DateOnly? OpenedOn { get; set; }

    /// <summary>Приказ Председателя Правления об утверждении состава комиссии (PRC-14).</summary>
    public string? CommissionOrderNumber { get; set; }
    public DateOnly? CommissionOrderDate { get; set; }

    /// <summary>Повторный конкурс сохраняет первоначальную комиссию (PRC-16).</summary>
    public int? PreviousTenderId { get; set; }
    public Tender? PreviousTender { get; set; }

    /// <summary>Основание, по которому конкурс не состоялся или отменён.</summary>
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CommissionMember> Commission { get; set; } = new List<CommissionMember>();
    public ICollection<TenderBid> Bids { get; set; } = new List<TenderBid>();
}

/// <summary>Член комиссии по закупке (PRC-14).</summary>
public class CommissionMember
{
    public int Id { get; set; }

    public int TenderId { get; set; }
    public Tender? Tender { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public CommissionRole Role { get; set; }

    /// <summary>Член Правления — от их числа зависит допустимость состава.</summary>
    public bool IsBoardMember { get; set; }

    /// <summary>Представитель УБУиО — обязателен для закупок свыше порога.</summary>
    public bool IsAccountant { get; set; }

    /// <summary>Участвовал в заседании — от этого считается кворум (PRC-15).</summary>
    public bool AttendedOpening { get; set; }

    /// <summary>Особое мнение члена комиссии фиксируется в протоколе.</summary>
    public string? DissentingOpinion { get; set; }

    /// <summary>
    /// Заключение эксперта по предмету закупки. Голоса у эксперта нет, поэтому его
    /// вклад — именно текст заключения и приложенный к нему файл.
    /// </summary>
    public string? Conclusion { get; set; }

    /// <summary>Файл заключения во вложениях документа закупки.</summary>
    public int? ConclusionAttachmentId { get; set; }

    public DateTime? ConclusionAt { get; set; }
}

/// <summary>Конкурсная заявка поставщика (PRC-15).</summary>
public class TenderBid
{
    public int Id { get; set; }

    public int TenderId { get; set; }
    public Tender? Tender { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal Price { get; set; }

    /// <summary>Когда заявка поступила — по ней решается, попадает ли она во вскрытие.</summary>
    public DateOnly SubmittedOn { get; set; }

    /// <summary>
    /// Заявка подана после окончательного срока. Такие к вскрытию не принимаются,
    /// но остаются в журнале: факт поступления должен быть зафиксирован.
    /// </summary>
    public bool IsLate { get; set; }

    /// <summary>Допущена к оценке: не опоздала, поставщик не в ЧС и не аффилирован без решения.</summary>
    public bool IsAdmitted { get; set; }

    public string? RejectionReason { get; set; }

    /// <summary>Оценка комиссии по совокупности цены и технических параметров.</summary>
    public int? Score { get; set; }

    public string? Specification { get; set; }

    public bool IsWinner { get; set; }
}
