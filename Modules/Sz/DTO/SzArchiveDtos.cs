namespace delosfera_server.Modules.Sz.DTO;

public class SzArchiveRequest
{
    /// <summary>Дело номенклатуры, в которое подшивается записка.</summary>
    public int NomenclatureCaseId { get; set; }

    /// <summary>Срок хранения; если не задан — берётся из дела.</summary>
    public int? StorageTermId { get; set; }
}

/// <summary>Карточка архивного хранения записки (SZ-07, GEN-09).</summary>
public class SzArchiveResponse
{
    public int SzId { get; set; }
    public string? RegNumber { get; set; }
    public string? Title { get; set; }
    public required string StatusCode { get; set; }

    public bool IsArchived { get; set; }
    public DateOnly? ArchivedOn { get; set; }

    public int? NomenclatureCaseId { get; set; }
    public string? CaseIndex { get; set; }
    public string? CaseTitle { get; set; }
    public DateOnly? CaseClosedOn { get; set; }

    public int? StorageTermId { get; set; }
    public string? StorageTerm { get; set; }

    /// <summary>Срок хранения в годах; пусто — хранение постоянное.</summary>
    public int? StorageYears { get; set; }

    /// <summary>Год, после которого документ можно уничтожить.</summary>
    public int? DestroyAfterYear { get; set; }

    /// <summary>Срок задан, но год уничтожения не посчитан: дело ещё не закрыто.</summary>
    public bool DestroyYearPending { get; set; }
}
