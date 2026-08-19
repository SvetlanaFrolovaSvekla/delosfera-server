using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>Тип поля в карточке настраиваемого типа документа (GEN-06).</summary>
public enum CustomFieldKind
{
    Text = 1,
    MultilineText = 2,
    Number = 3,
    Money = 4,
    Date = 5,
    Checkbox = 6,

    /// <summary>Значение из справочника администратора (GEN-07).</summary>
    Dictionary = 7,

    /// <summary>Сотрудник банка.</summary>
    User = 8,

    /// <summary>Структурное подразделение.</summary>
    OrgUnit = 9,
}

/// <summary>
/// Тип документа, заведённый администратором заказчика (GEN-06).
///
/// Встроенные контуры (служебные записки, ВНД, закупки) остаются в коде: у них своя
/// логика — матрица полномочий, бумажный носитель, протоколы. Настраиваемые типы
/// закрывают остальное: приказы, входящая и исходящая корреспонденция, договоры вне
/// закупок. Их карточка описывается полями, а движение — шаблоном маршрута, который
/// уже умеет всё, что нужно.
/// </summary>
public class DocumentTypeDefinition : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Системное имя типа: латиницей, попадает в номер и в адреса карточек.</summary>
    public required string Code { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Шаблон маршрута, по которому документ этого типа идёт на согласование.
    /// Не задан — документ живёт только как учётная карточка без движения.
    /// </summary>
    public int? RouteTemplateId { get; set; }
    public RouteTemplate? RouteTemplate { get; set; }

    /// <summary>Маска регистрационного номера, например «ПР-{year}-{seq:D4}».</summary>
    public string? NumberPattern { get; set; }

    /// <summary>Выключенный тип не предлагается при создании, но карточки остаются.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<DocumentTypeField> Fields { get; set; } = new List<DocumentTypeField>();
}

/// <summary>Поле карточки настраиваемого типа документа.</summary>
public class DocumentTypeField : IAuditableEntity
{
    public int Id { get; set; }

    public int DefinitionId { get; set; }
    public DocumentTypeDefinition? Definition { get; set; }

    /// <summary>Имя поля в данных карточки — по нему значение хранится и ищется.</summary>
    public required string Code { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public CustomFieldKind Kind { get; set; }

    /// <summary>Справочник для поля вида «Значение из справочника».</summary>
    public int? DictionaryId { get; set; }
    public Dictionaries.Models.CustomDictionary? Dictionary { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>Поле показывается в журнале как колонка по умолчанию (GEN-10).</summary>
    public bool ShowInList { get; set; }

    public int Order { get; set; }

    public string? Hint { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Представление журнала: какие колонки видит пользователь и в каком порядке (GEN-10).
///
/// Личное представление принадлежит сотруднику, общее заводит администратор для всех.
/// Разделение важно: делопроизводителю и куратору нужны разные колонки одного журнала,
/// и попытка договориться об одном общем наборе заканчивается тем, что неудобно обоим.
/// </summary>
public class ListView : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Журнал, к которому относится представление: sz, prc, meetings, vnd или код типа.</summary>
    public required string Scope { get; set; }

    public required string Name { get; set; }

    /// <summary>Коды колонок по порядку — json-массив.</summary>
    public required string Columns { get; set; }

    /// <summary>Условия отбора в том же виде, в каком их принимает журнал — json.</summary>
    public string? Filter { get; set; }

    /// <summary>Владелец личного представления; null — общее представление банка.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Открывать этот набор колонок по умолчанию.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentTypeDefinitionConfiguration : IEntityTypeConfiguration<DocumentTypeDefinition>
{
    public void Configure(EntityTypeBuilder<DocumentTypeDefinition> b)
    {
        b.ToTable("document_type_definition");
        b.HasIndex(x => x.Code).IsUnique();

        b.HasOne(x => x.RouteTemplate)
            .WithMany()
            .HasForeignKey(x => x.RouteTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Fields)
            .WithOne(f => f.Definition!)
            .HasForeignKey(f => f.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentTypeFieldConfiguration : IEntityTypeConfiguration<DocumentTypeField>
{
    public void Configure(EntityTypeBuilder<DocumentTypeField> b)
    {
        b.ToTable("document_type_field");
        b.Property(x => x.Kind).HasConversion<int>();

        // Код поля уникален внутри типа: по нему читаются значения карточки.
        b.HasIndex(x => new {x.DefinitionId, x.Code}).IsUnique();

        b.HasOne(x => x.Dictionary)
            .WithMany()
            .HasForeignKey(x => x.DictionaryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ListViewConfiguration : IEntityTypeConfiguration<ListView>
{
    public void Configure(EntityTypeBuilder<ListView> b)
    {
        b.ToTable("list_view");
        b.Property(x => x.Columns).HasColumnType("jsonb");
        b.Property(x => x.Filter).HasColumnType("jsonb");

        b.HasIndex(x => new {x.Scope, x.UserId});

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
