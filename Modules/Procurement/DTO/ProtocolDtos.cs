using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Строка сравнительной таблицы в печатной форме протокола.</summary>
public class ProtocolRowDto
{
    public int Order { get; set; }
    public required string SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }
    public decimal Price { get; set; }
    public string? Specification { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? PaymentTerms { get; set; }
    public required string InitiatorConclusion { get; set; }
    public bool IsWinner { get; set; }
}

/// <summary>Подпись стороны под протоколом.</summary>
public class ProtocolSignatureDto
{
    public ProtocolSignerRole Role { get; set; }
    public required string RoleTitle { get; set; }
    public required string UserName { get; set; }
    public string? Position { get; set; }
    public required string LevelTitle { get; set; }
    public DateTime At { get; set; }
    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }
}

/// <summary>Протокол закупки целиком — данные печатной формы (приложение к Положению).</summary>
public class ProtocolDto
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public string? RegNumber { get; set; }
    public DateOnly ProtocolDate { get; set; }

    public ProtocolStatus Status { get; set; }
    public required string StatusTitle { get; set; }

    public required string MethodTitle { get; set; }
    public required string Subject { get; set; }
    public string? InitiatorUnitTitle { get; set; }

    public string? MainSupplierTitle { get; set; }
    public decimal? MainAmount { get; set; }
    public string? ReserveSupplierTitle { get; set; }
    public decimal? ReserveAmount { get; set; }

    public string? BudgetNote { get; set; }
    public string? ExpertOpinion { get; set; }
    public string? DissentingOpinion { get; set; }
    public string? Recommendations { get; set; }
    public string? SelectionBasis { get; set; }

    /// <summary>Победитель не с наименьшей ценой — требуется основание выбора (PRC-12).</summary>
    public bool RequiresSelectionBasis { get; set; }

    public List<ProtocolRowDto> Rows { get; set; } = [];
    public List<ProtocolSignatureDto> Signatures { get; set; } = [];

    /// <summary>Чего не хватает для подписания.</summary>
    public List<string> Blockers { get; set; } = [];

    /// <summary>
    /// Данные сравнительной таблицы изменились после формирования — протокол
    /// нужно пересобрать, иначе он расходится с закупкой.
    /// </summary>
    public bool IsOutdated { get; set; }
}

/// <summary>Правка разделов протокола, заполняемых вручную.</summary>
public class ProtocolUpdateRequest
{
    public string? BudgetNote { get; set; }
    public string? ExpertOpinion { get; set; }
    public string? DissentingOpinion { get; set; }
    public string? Recommendations { get; set; }
    public string? SelectionBasis { get; set; }
}

/// <summary>Подписание протокола.</summary>
public class ProtocolSignRequest
{
    public ProtocolSignerRole Role { get; set; }

    /// <summary>0 — ПЭП (виза), 1 — КЭП (юридически значимая подпись).</summary>
    public int Level { get; set; } = 1;
}
