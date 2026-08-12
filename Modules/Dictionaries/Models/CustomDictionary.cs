using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/// <summary>
/// Справочник, заведённый администратором заказчика (GEN-07).
///
/// Встроенные справочники (виды ВНД, уровни секретности, органы утверждения) остаются
/// отдельными таблицами: на них завязана логика контуров, и превращать их в строки
/// общего хранилища — значит потерять внешние ключи и проверки целостности.
///
/// Здесь живут справочники, которых нет в ТЗ и не будет в коде: банк заводит их сам
/// под свои процессы — «Виды обучения», «Категории обращений», «Каналы поступления».
/// </summary>
public class CustomDictionary : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Системное имя для ссылок из настроек: латиницей, без пробелов. По нему тип
    /// документа связывает своё поле со справочником.
    /// </summary>
    public required string Code { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public string? Description { get; set; }

    /// <summary>Справочник иерархический — значения могут иметь родителя.</summary>
    public bool IsHierarchical { get; set; }

    /// <summary>
    /// Выключенный справочник не предлагается в новых карточках, но остаётся у старых:
    /// удалять справочник, на который уже ссылаются документы, нельзя.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CustomDictionaryItem> Items { get; set; } = new List<CustomDictionaryItem>();
}

/// <summary>Значение справочника администратора.</summary>
public class CustomDictionaryItem : IAuditableEntity
{
    public int Id { get; set; }

    public int DictionaryId { get; set; }
    public CustomDictionary? Dictionary { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Код значения — по нему на значение ссылаются интеграции и отчёты.</summary>
    public string? Code { get; set; }

    public int? ParentId { get; set; }
    public CustomDictionaryItem? Parent { get; set; }

    public int Order { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CustomDictionaryConfiguration : IEntityTypeConfiguration<CustomDictionary>
{
    public void Configure(EntityTypeBuilder<CustomDictionary> b)
    {
        b.ToTable("dictionary_custom");

        // Код — точка ссылки из настроек типов документов, дубли недопустимы.
        b.HasIndex(x => x.Code).IsUnique();

        b.HasMany(x => x.Items)
            .WithOne(i => i.Dictionary!)
            .HasForeignKey(i => i.DictionaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomDictionaryItemConfiguration : IEntityTypeConfiguration<CustomDictionaryItem>
{
    public void Configure(EntityTypeBuilder<CustomDictionaryItem> b)
    {
        b.ToTable("dictionary_custom_item");
        b.HasIndex(x => new {x.DictionaryId, x.Order});

        b.HasOne(x => x.Parent)
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
