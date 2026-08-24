using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using delosfera_server.Modules.Settings.Models;

namespace delosfera_server.Modules.Settings.Services;

/// <summary>
/// Собирает изменения настроек прямо из отслеживаемых сущностей.
///
/// Перехват в одном месте, а не вызов из каждой службы. Справочников девять,
/// служб столько же, и десятый справочник заведут, забыв про журнал, — это
/// вопрос времени, а не внимательности. Здесь же новый справочник достаточно
/// внести в список областей, и он начнёт записываться сам.
/// </summary>
public static class SettingsChangeCollector
{
    /// <summary>
    /// За чем следим. Ключ — имя класса, значение — человеческое название
    /// области: журнал читает администратор банка, а не разработчик.
    /// </summary>
    private static readonly Dictionary<string, string> Areas = new()
    {
        ["OrganizationUnit"] = "Подразделения",
        ["Position"] = "Должности",
        ["ApprovalBody"] = "Органы утверждения",
        ["TypeVnd"] = "Виды ВНД",
        ["SecurityLevel"] = "Грифы секретности",
        ["Rubric"] = "Рубрикатор",
        ["Keyword"] = "Ключевые слова",
        ["UserGroup"] = "Группы пользователей",
        ["NomenclatureCase"] = "Номенклатура дел",
        ["StorageTerm"] = "Сроки хранения",

        ["Role"] = "Роли и права",

        ["SzKind"] = "Виды служебных записок",
        ["RouteTemplate"] = "Шаблоны маршрутов",
        ["CoordinationDefaultApprover"] = "Обязательные согласующие",

        ["ProcurementMethod"] = "Способы закупки",
        ["AuthorityMatrixRule"] = "Матрица полномочий",
        ["ProcurementParameter"] = "Параметры закупок",

        ["TrustedCertificateAuthority"] = "Доверенные удостоверяющие центры",
        ["SigningSettings"] = "Настройки подписания",
        ["SimpleSignatureRegulation"] = "Регламент простой подписи",

        ["Correspondent"] = "Корреспонденты",
        ["RecurringObligation"] = "Регулярные обязательства",

        ["ActualizationSettings"] = "Настройки актуализации",
        ["DocumentTypeDefinition"] = "Типы документов",
        ["CustomDictionary"] = "Пользовательские справочники",
    };

    /// <summary>
    /// Настройки записи изменений.
    ///
    /// Кириллицу не экранируем: по умолчанию сериализатор пишет «Было» как
    /// «\u0411\u044B\u043B\u043E» — журнал становится нечитаемым при взгляде
    /// в саму колонку и занимает вшестеро больше места. Значения сюда попадают
    /// из справочников банка, то есть почти всегда русские.
    ///
    /// Послабление безопасно: строка кладётся в jsonb и наружу отдаётся уже
    /// разобранной, в разметку страницы она не подставляется.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Поля, которые в журнал не пишем. Служебные отметки времени меняются при
    /// каждом сохранении и превратили бы журнал в шум; поисковый вектор —
    /// вычисляемый, его никто не правит.
    /// </summary>
    private static readonly HashSet<string> Skip =
        ["CreatedAt", "UpdatedAt", "SearchVector", "ConcurrencyToken"];

    /// <summary>
    /// Готовит записи журнала по текущему состоянию отслеживания. Вызывается до
    /// сохранения: после него старые значения уже потеряны.
    ///
    /// У добавленных записей идентификатора ещё нет — его проставляют после
    /// сохранения, поэтому возвращается пара «запись журнала и сущность».
    /// </summary>
    public static List<(SettingsChange Change, EntityEntry Entry)> Collect(
        ChangeTracker tracker, int? userId)
    {
        var result = new List<(SettingsChange, EntityEntry)>();
        var now = DateTime.UtcNow;

        foreach (var entry in tracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var typeName = entry.Metadata.ClrType.Name;
            if (!Areas.TryGetValue(typeName, out var area)) continue;

            var kind = entry.State switch
            {
                EntityState.Added => SettingsChangeKind.Added,
                EntityState.Deleted => SettingsChangeKind.Deleted,
                _ => SettingsChangeKind.Modified,
            };

            var changes = Describe(entry, kind);

            // Правка, не задевшая ни одного значимого поля, — это сохранение
            // ради отметки времени. Записывать её незачем.
            if (kind == SettingsChangeKind.Modified && changes.Count == 0) continue;

            var change = new SettingsChange
            {
                Area = area,
                EntityType = typeName,
                EntityId = kind == SettingsChangeKind.Added ? 0 : ReadId(entry),
                EntityTitle = ReadTitle(entry),
                Kind = kind,
                ChangesJson = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, JsonOptions),
                UserId = userId,
                At = now,
            };

            result.Add((change, entry));
        }

        return result;
    }

    /// <summary>Проставляет идентификаторы добавленным записям — после сохранения они уже есть.</summary>
    public static void FillIds(List<(SettingsChange Change, EntityEntry Entry)> pending)
    {
        foreach (var (change, entry) in pending)
        {
            if (change.EntityId == 0)
                change.EntityId = ReadId(entry);
        }
    }

    private static List<FieldChange> Describe(EntityEntry entry, SettingsChangeKind kind)
    {
        var changes = new List<FieldChange>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (Skip.Contains(name)) continue;

            // Ключ в поля не пишем. У добавленной записи настоящего ключа ещё
            // нет — EF до сохранения подставляет временный отрицательный, и в
            // журнале появлялось «Id −2147482647». Сам идентификатор и так
            // лежит в отдельной колонке записи журнала.
            if (property.Metadata.IsPrimaryKey()) continue;

            switch (kind)
            {
                case SettingsChangeKind.Modified when property.IsModified:
                {
                    var before = Format(property.OriginalValue);
                    var after = Format(property.CurrentValue);

                    // EF помечает поле изменённым и когда значение то же самое —
                    // например, при переприсваивании. Такие в журнал не идут.
                    if (before == after) continue;

                    changes.Add(new FieldChange(name, before, after));
                    break;
                }

                case SettingsChangeKind.Added when property.CurrentValue is not null:
                    changes.Add(new FieldChange(name, null, Format(property.CurrentValue)));
                    break;

                case SettingsChangeKind.Deleted when property.OriginalValue is not null:
                    changes.Add(new FieldChange(name, Format(property.OriginalValue), null));
                    break;
            }
        }

        return changes;
    }

    /// <summary>
    /// Название записи: пробуем поля, которыми её обычно зовут. У сущностей без
    /// названия — например, у настроек в единственном экземпляре — его нет, и
    /// это нормально.
    /// </summary>
    private static string? ReadTitle(EntityEntry entry)
    {
        foreach (var candidate in new[] { "TitleRu", "Title", "Name", "Code" })
        {
            var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == candidate);
            if (property is null) continue;

            var value = entry.State == EntityState.Deleted
                ? property.OriginalValue
                : property.CurrentValue;

            if (value is string text && !string.IsNullOrWhiteSpace(text))
                return text.Length <= 300 ? text : text[..300];
        }

        return null;
    }

    private static int ReadId(EntityEntry entry)
    {
        var key = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        if (key is null) return 0;

        var value = entry.State == EntityState.Deleted ? key.OriginalValue : key.CurrentValue;

        return value switch
        {
            int number => number,
            long big and >= int.MinValue and <= int.MaxValue => (int)big,
            _ => 0,
        };
    }

    /// <summary>
    /// Значение строкой для журнала. Длинное обрезается: журнал читают глазами,
    /// и текст положения на двадцать килобайт в нём бесполезен.
    /// </summary>
    private static string? Format(object? value)
    {
        if (value is null) return null;

        var text = value switch
        {
            DateTime dt => dt.ToString("dd.MM.yyyy HH:mm"),
            DateOnly d => d.ToString("dd.MM.yyyy"),
            bool flag => flag ? "да" : "нет",
            int[] numbers => string.Join(", ", numbers),
            _ => value.ToString() ?? "",
        };

        return text.Length <= 500 ? text : text[..500] + "…";
    }

    private sealed record FieldChange(string Field, string? Before, string? After);
}
