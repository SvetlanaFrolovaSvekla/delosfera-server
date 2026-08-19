using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/// <summary>
/// Срок хранения документа по номенклатуре дел (GEN-09): «5 лет», «75 лет», «Постоянно».
/// Перечень и сроки для служебных записок — открытый вопрос В-2 к Заказчику,
/// поэтому справочник пополняется администратором, а не зашит в код.
/// </summary>
public class StorageTerm : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    /// <summary>Краткий код для печатных форм и описей: «5л», «75л», «Пост».</summary>
    public required string Code { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Срок в годах; null — хранить постоянно, дата уничтожения не считается.</summary>
    public int? Years { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
