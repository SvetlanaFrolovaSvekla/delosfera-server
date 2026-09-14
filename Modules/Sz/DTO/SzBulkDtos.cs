namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Массовая сдача записок в архив (СЗ-7): пачка в одно дело номенклатуры.</summary>
public class SzBulkArchiveRequest
{
    public List<int> Ids { get; set; } = [];

    /// <summary>Дело, в которое подшивается вся пачка.</summary>
    public int NomenclatureCaseId { get; set; }

    /// <summary>Срок хранения; если не задан — берётся из дела.</summary>
    public int? StorageTermId { get; set; }
}

/// <summary>
/// Итог массовой операции: обработали столько-то, часть могла не пройти. Ошибки
/// перечислены поимённо — массовое действие не должно молча терять записки.
/// </summary>
public class SzBulkResult
{
    public int Requested { get; set; }
    public int Succeeded { get; set; }
    public List<SzBulkFailure> Failed { get; set; } = [];
}

/// <summary>Записка, которую не удалось обработать, с причиной.</summary>
public class SzBulkFailure
{
    public int SzId { get; set; }
    public string? RegNumber { get; set; }
    public required string Message { get; set; }
}
