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

    /// <summary>Доверители (любой из). Расширенный поиск реестра.</summary>
    public List<int>? GrantorUserIds { get; set; }

    /// <summary>Представители-сотрудники (любой из).</summary>
    public List<int>? HolderUserIds { get; set; }

    /// <summary>
    /// Часть ФИО представителя, как оно записано в доверенности. Единственный способ
    /// найти доверенность на стороннее лицо: учётной записи у него нет.
    /// </summary>
    public string? HolderName { get; set; }

    /// <summary>Сотрудник банка или стороннее лицо.</summary>
    public PoaHolderKind? HolderKind { get; set; }

    /// <summary>Часть регистрационного номера.</summary>
    public string? RegNumber { get; set; }

    /// <summary>Фраза из текста полномочий.</summary>
    public string? Powers { get; set; }

    /// <summary>Только с правом передоверия.</summary>
    public bool? AllowsDelegation { get; set; }

    /// <summary>Дата выдачи: с — по (любая граница может быть пустой).</summary>
    public DateOnly? IssuedFrom { get; set; }
    public DateOnly? IssuedTo { get; set; }

    /// <summary>Дата окончания срока: с — по.</summary>
    public DateOnly? ValidToFrom { get; set; }
    public DateOnly? ValidToTo { get; set; }

    /// <summary>
    /// Действовавшие в указанный день — «кто был вправе подписать тогда». Считается по
    /// датам, а не по нынешнему состоянию: истёкшая или отозванная позже доверенность
    /// в тот день действовала.
    /// </summary>
    public DateOnly? ValidOn { get; set; }

    /// <summary>
    /// Общая строка поиска: слова ищутся порознь (должны найтись все, в любом порядке),
    /// каждое — в ФИО представителя или доверителя, номере и тексте полномочий.
    /// </summary>
    public string? Text { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Скан доверенности.</summary>
public class PoaFileDto
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public string? FileName { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
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
    public int? RevokedByUserId { get; set; }
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

/// <summary>Выдача бумажного оригинала на руки (ЗВ-1).</summary>
public class PoaHandoverRequest
{
    /// <summary>Где будет находиться оригинал / у кого. Необязательно.</summary>
    public string? Location { get; set; }
}

public class PoaRevokeRequest
{
    public string Reason { get; set; } = "";

    /// <summary>Дата отзыва. Пусто — сегодня.</summary>
    public DateOnly? On { get; set; }
}
