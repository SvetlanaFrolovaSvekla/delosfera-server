namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Строка сравнительной таблицы предложений (PRC-09).</summary>
public class ProposalDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }

    public decimal Price { get; set; }
    public int? DeliveryDays { get; set; }
    public int? WarrantyMonths { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Specification { get; set; }
    public DateOnly ReceivedOn { get; set; }

    public bool? MeetsRequirements { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsWinner { get; set; }

    /// <summary>Ссылка на облако банка с приложениями к предложению.</summary>
    public string? ExternalLink { get; set; }

    /// <summary>Файлы предложения: письмо поставщика, смета, спецификация.</summary>
    public List<ProposalFileDto> Files { get; set; } = [];

    /// <summary>Насколько предложение дороже минимального, в процентах.</summary>
    public decimal? PriceDeltaPercent { get; set; }

    /// <summary>Поставщик в чёрном списке — к отбору не допускается (PRC-17).</summary>
    public bool SupplierBlacklisted { get; set; }
    public bool SupplierAffiliated { get; set; }
    public bool? SupplierReliable { get; set; }
}

/// <summary>Сравнительная таблица и состояние отбора по заявке.</summary>
public class ProposalComparisonDto
{
    public int RequestId { get; set; }
    public required string MethodTitle { get; set; }

    /// <summary>Сколько предложений требует способ закупки (для простой — 3).</summary>
    public int MinProposals { get; set; }
    public int ReceivedCount { get; set; }

    /// <summary>Предложения, допущенные к отбору: не отклонены и поставщик не в ЧС.</summary>
    public int EligibleCount { get; set; }

    public List<ProposalDto> Proposals { get; set; } = [];

    /// <summary>Предложение с наименьшей ценой среди допущенных — кандидат в победители.</summary>
    public int? RecommendedProposalId { get; set; }

    public decimal? LowestPrice { get; set; }

    /// <summary>Требуется ли протокол закупки при этой сумме (PRC-10).</summary>
    public bool ProtocolRequired { get; set; }

    /// <summary>Чего не хватает для определения победителя.</summary>
    public List<string> Blockers { get; set; } = [];
}

/// <summary>Регистрация коммерческого предложения.</summary>
public class ProposalCreateRequest
{
    /// <summary>Существующий поставщик; пусто — заводится новый по названию и ИНН.</summary>
    public int? SupplierId { get; set; }

    public string? SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }

    public decimal Price { get; set; }
    public int? DeliveryDays { get; set; }
    public int? WarrantyMonths { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Specification { get; set; }
    public DateOnly? ReceivedOn { get; set; }
}

/// <summary>Заключение о соответствии предложения техническим требованиям (PRC-11).</summary>
public class ProposalFileDto
{
    public int Id { get; set; }
    public int AttachmentId { get; set; }
    public required string FileName { get; set; }
    public long Size { get; set; }
}

/// <summary>Источники предложения: файлы из карточки закупки и ссылка на облако.</summary>
public class ProposalSourcesRequest
{
    /// <summary>Вложения документа закупки, относящиеся к этому предложению.</summary>
    public List<int> AttachmentIds { get; set; } = [];

    /// <summary>Ссылка на nextcloud.keremetbank.kg. Пусто — ссылку убрать.</summary>
    public string? ExternalLink { get; set; }
}

public class ProposalVerdictRequest
{
    public bool MeetsRequirements { get; set; }
    public string? RejectionReason { get; set; }
}
