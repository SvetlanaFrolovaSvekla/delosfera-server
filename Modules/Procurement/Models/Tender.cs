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

    /// <summary>Дата, с которой конкурс считается объявленным.</summary>
    public DateOnly? PublishedOn { get; set; }

    /// <summary>
    /// Где объявление выложено на самом деле.
    ///
    /// Система собирает текст объявления, но выкладывает его человек: у сайта
    /// Банка и у tenders.kg нет согласованного API. Пока отметки нет, объявление
    /// считается неразмещённым — иначе выходит, что система выдала текст и
    /// забыла о нём, а срок приёма заявок уже идёт.
    /// </summary>
    public string? PublishedAt { get; set; }

    /// <summary>Когда сотрудник отметил размещение.</summary>
    public DateTime? PublicationConfirmedAt { get; set; }

    public int? PublicationConfirmedByUserId { get; set; }
    public User? PublicationConfirmedBy { get; set; }

    /// <summary>
    /// Окончательный срок приёма заявок. Конкурсный период — не менее 5 рабочих дней
    /// с публикации; заявки после срока к вскрытию не принимаются (PRC-15).
    /// </summary>
    public DateOnly? SubmissionDeadline { get; set; }

    public DateOnly? OpenedOn { get; set; }

    /// <summary>
    /// Заседание комиссии по вскрытию и решению. Решения принимаются очно (п. 24.2),
    /// поэтому у заседания есть дата: без неё нельзя ни собрать людей, ни объяснить,
    /// почему голоса внесены задним числом.
    /// </summary>
    public DateOnly? MeetingDate { get; set; }

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

    /// <summary>Переносы заседания — по одному на каждый сдвиг.</summary>
    public ICollection<TenderMeetingChange> MeetingChanges { get; set; } = new List<TenderMeetingChange>();
}

/// <summary>
/// Перенос заседания комиссии.
///
/// Заседание срывается по обычным причинам: не собрался кворум, заболел
/// председатель, поставщик просит продлить приём. Сектор закупок переносит дату —
/// но перенос остаётся записью, а не тихой правкой поля: по срокам закупки потом
/// задают вопросы, и «почему решение приняли на три недели позже» должно иметь
/// письменный ответ.
/// </summary>
public class TenderMeetingChange
{
    public int Id { get; set; }

    public int TenderId { get; set; }
    public Tender? Tender { get; set; }

    /// <summary>С какой даты перенесли. Пусто — заседание назначается впервые.</summary>
    public DateOnly? FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>Основание переноса — печатается в протоколе.</summary>
    public required string Reason { get; set; }

    public int ByUserId { get; set; }
    public User? By { get; set; }

    public DateTime At { get; set; }
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

    // Постоянные члены комиссии по п. 120 Положения: УБУиО, Юридическая служба и
    // Управление безопасности. Требование безусловное — порога суммы у него нет.
    // Признаки хранятся у члена комиссии, а не выводятся из подразделения:
    // в комиссию входит «руководитель/работник», а сотрудник может числиться
    // в другом СП и исполнять эту роль по приказу.

    /// <summary>Представитель УБУиО.</summary>
    public bool IsAccountant { get; set; }

    /// <summary>Представитель Юридической службы.</summary>
    public bool IsLegal { get; set; }

    /// <summary>Представитель Управления безопасности.</summary>
    public bool IsSecurity { get; set; }

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

    /// <summary>Голоса членов комиссии по этой заявке.</summary>
    public ICollection<CommissionVote> Votes { get; set; } = new List<CommissionVote>();
}

/// <summary>Как проголосовал член комиссии (п. 24.2 Положения).</summary>
public enum VoteChoice
{
    For = 1,
    Against = 2,
    Abstained = 3,
}

/// <summary>
/// Голос члена комиссии по конкретной заявке (п. 24.2 Положения).
///
/// Заседание очное, поэтому голоса вносит в систему секретарь или Сектор закупок
/// по итогам заседания, а не каждый член сам. Отсюда отдельное поле «кто внёс»:
/// подпись под протоколом ставит член комиссии, но запись в системе делает не он,
/// и путать эти два действия нельзя.
/// </summary>
public class CommissionVote
{
    public int Id { get; set; }

    public int BidId { get; set; }
    public TenderBid? Bid { get; set; }

    public int MemberId { get; set; }
    public CommissionMember? Member { get; set; }

    public VoteChoice Choice { get; set; }

    /// <summary>Кто внёс голос в систему — секретарь комиссии или Сектор закупок.</summary>
    public int RecordedByUserId { get; set; }
    public User? RecordedBy { get; set; }

    public DateTime At { get; set; }
}
