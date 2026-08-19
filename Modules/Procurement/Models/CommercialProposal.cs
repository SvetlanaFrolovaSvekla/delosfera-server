using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Коммерческое предложение поставщика (PRC-09). По простой закупке собирается
/// не менее трёх; из зарегистрированных предложений система строит сравнительную
/// таблицу и определяет победителя по цене среди технически подходящих (PRC-12).
/// </summary>
public class CommercialProposal : IAuditableEntity
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Цена предложения в сомах.</summary>
    public decimal Price { get; set; }

    /// <summary>Срок поставки в днях.</summary>
    public int? DeliveryDays { get; set; }

    /// <summary>Гарантийный срок в месяцах.</summary>
    public int? WarrantyMonths { get; set; }

    /// <summary>Условия оплаты: «предоплата 30%», «по факту поставки».</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Технические характеристики предложения — идут в таблицу протокола.</summary>
    public string? Specification { get; set; }

    /// <summary>Файл КП во вложениях документа закупки.</summary>
    public int? AttachmentId { get; set; }

    /// <summary>
    /// Ссылка на облако банка (nextcloud.keremetbank.kg) для объёмных приложений:
    /// поставщики шлют сметы и каталоги, которые в почтовое вложение не помещаются.
    /// </summary>
    public string? ExternalLink { get; set; }

    /// <summary>Файлы предложения — одно КП приходит пачкой: письмо, смета, спецификация.</summary>
    public ICollection<ProposalFile> Files { get; set; } = new List<ProposalFile>();

    /// <summary>Дата поступления предложения.</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>
    /// Заключение инициатора о соответствии техническим требованиям (PRC-11).
    /// Null — заключение ещё не давали; false выводит предложение из отбора.
    /// </summary>
    public bool? MeetsRequirements { get; set; }

    /// <summary>Причина отклонения предложения — печатается в протоколе.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Предложение признано победившим (PRC-12).</summary>
    public bool IsWinner { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Файл коммерческого предложения. Сам файл лежит во вложениях документа закупки —
/// здесь только связь: так к КП можно приложить несколько документов, а хранилище,
/// хеши и подписи остаются общими для всей карточки.
/// </summary>
public class ProposalFile
{
    public int Id { get; set; }

    public int ProposalId { get; set; }
    public CommercialProposal? Proposal { get; set; }

    /// <summary>Вложение документа закупки (Documents.DocumentAttachment).</summary>
    public int DocumentAttachmentId { get; set; }

    public DateTime AddedAt { get; set; }
}
