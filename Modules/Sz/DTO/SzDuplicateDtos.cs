namespace delosfera_server.Modules.Sz.DTO;

/// <summary>
/// Возможный дубликат служебной записки (СК-5): похожая записка того же автора и вида,
/// заведённая недавно. Предупреждение, а не запрет: иногда две похожие записки нужны
/// (повторная заявка, разные объекты), поэтому решает автор, а система лишь показывает,
/// что что-то очень похожее уже есть.
/// </summary>
public class SzDuplicateDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public required string Title { get; set; }
    public required string StatusTitle { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Насколько тема совпадает, % (по словам).</summary>
    public int SimilarityPercent { get; set; }
}
