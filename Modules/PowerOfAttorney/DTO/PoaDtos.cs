using delosfera_server.Modules.PowerOfAttorney.Models;

namespace delosfera_server.Modules.PowerOfAttorney.DTO;

/// <summary>Заведение и правка доверенности.</summary>
public class PoaSaveRequest
{
    public DateOnly IssuedOn { get; set; }

    /// <summary>Доверитель — должностное лицо, подписывающее от имени банка.</summary>
    public int GrantorUserId { get; set; }

    /// <summary>Основание для передоверия.</summary>
    public int? ParentPoaId { get; set; }

    public PoaHolderKind HolderKind { get; set; } = PoaHolderKind.Employee;
    public int? HolderUserId { get; set; }
    public string HolderName { get; set; } = "";
    public string? HolderPosition { get; set; }
    public int? HolderUnitId { get; set; }
    public string? HolderIdentityDocument { get; set; }

    public string Powers { get; set; } = "";
    public bool AllowsDelegation { get; set; }
    public decimal? AmountLimit { get; set; }
    public string? AmountCurrency { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    public string? OriginalLocation { get; set; }
}

public class PoaFilterRequest
{
    public List<PoaStatus>? Statuses { get; set; }
    public int? HolderUserId { get; set; }
    public int? GrantorUserId { get; set; }
    public int? UnitId { get; set; }

    /// <summary>Действующие на указанный день — «кто был вправе подписать тогда».</summary>
    public DateOnly? ValidOn { get; set; }

    /// <summary>Поиск по ФИО представителя, номеру и тексту полномочий.</summary>
    public string? Text { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PoaDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public DateOnly IssuedOn { get; set; }
    public string Status { get; set; } = "";

    public int GrantorUserId { get; set; }
    public string? GrantorName { get; set; }

    public int? ParentPoaId { get; set; }
    public string? ParentRegNumber { get; set; }

    public string HolderKind { get; set; } = "";
    public int? HolderUserId { get; set; }
    public string HolderName { get; set; } = "";
    public string? HolderPosition { get; set; }
    public int? HolderUnitId { get; set; }
    public string? HolderUnit { get; set; }
    public string? HolderIdentityDocument { get; set; }

    public string Powers { get; set; } = "";
    public bool AllowsDelegation { get; set; }
    public decimal? AmountLimit { get; set; }
    public string? AmountCurrency { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    public DateTime? SignedAt { get; set; }
    public DateOnly? RevokedOn { get; set; }
    public string? RevokeReason { get; set; }
    public string? RevokedBy { get; set; }

    public string? OriginalLocation { get; set; }
    public DateTime? OriginalHandedAt { get; set; }
    public DateTime? OriginalReturnedAt { get; set; }

    public int FileCount { get; set; }
    public int ChildCount { get; set; }
}

public class PoaListResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<PoaDto> Items { get; set; } = [];
}

public class PoaRevokeRequest
{
    public string Reason { get; set; } = "";

    /// <summary>Дата отзыва. Пусто — сегодня.</summary>
    public DateOnly? On { get; set; }
}
