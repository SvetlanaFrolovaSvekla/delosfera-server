namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Поставщик в реестре (PRC-07/17).</summary>
public class SupplierDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Inn { get; set; }
    public string? Address { get; set; }
    public string? DirectorName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsAffiliated { get; set; }

    public bool? IsReliable { get; set; }
    public DateOnly? ReliabilityCheckedOn { get; set; }
    public bool HasTaxClearance { get; set; }
    public bool HasSocialFundClearance { get; set; }

    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public DateOnly? BlacklistedUntil { get; set; }

    /// <summary>Срок ограничения истёк — запись осталась, но допуск восстановлен.</summary>
    public bool BlacklistExpired { get; set; }
}

public class SupplierUpsertRequest
{
    public int? Id { get; set; }
    public required string Title { get; set; }
    public string? Inn { get; set; }
    public string? Address { get; set; }
    public string? DirectorName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsAffiliated { get; set; }
}

/// <summary>Включение в чёрный список по приложению №4 к Положению.</summary>
public class BlacklistRequest
{
    public required string Reason { get; set; }

    /// <summary>Срок ограничения; пусто — бессрочно, до отдельного решения.</summary>
    public DateOnly? Until { get; set; }
}

/// <summary>Заключение ДБ о благонадёжности поставщика (PRC-07).</summary>
public class ReliabilityRequest
{
    public bool IsReliable { get; set; }
    public bool HasTaxClearance { get; set; }
    public bool HasSocialFundClearance { get; set; }
}
