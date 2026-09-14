namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Одна оценка поставщика (ЗК-9).</summary>
public class SupplierRatingDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public int? ContractId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Оценки поставщика вместе со средним баллом.</summary>
public class SupplierRatingsResponse
{
    public double? Average { get; set; }
    public int Count { get; set; }
    public List<SupplierRatingDto> Items { get; set; } = [];
}

/// <summary>Поставить оценку поставщику.</summary>
public class AddSupplierRatingRequest
{
    /// <summary>Балл 1..5.</summary>
    public int Score { get; set; }
    public string? Comment { get; set; }

    /// <summary>Договор, к которому относится оценка (необязательно).</summary>
    public int? ContractId { get; set; }
}
