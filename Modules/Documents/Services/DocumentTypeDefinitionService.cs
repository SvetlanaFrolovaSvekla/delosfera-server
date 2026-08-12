using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

public interface IDocumentTypeDefinitionService
{
    Task<List<DocumentTypeDefinitionDto>> ListAsync(bool includeInactive = false);
    Task<DocumentTypeDefinitionDto> GetAsync(int id);
    Task<DocumentTypeDefinitionDto> CreateAsync(DocumentTypeSaveRequest request);
    Task<DocumentTypeDefinitionDto> UpdateAsync(int id, DocumentTypeSaveRequest request);
    Task DeleteAsync(int id);

    Task<DocumentTypeDefinitionDto> AddFieldAsync(int definitionId, DocumentTypeFieldRequest request);
    Task<DocumentTypeDefinitionDto> UpdateFieldAsync(int fieldId, DocumentTypeFieldRequest request);
    Task<DocumentTypeDefinitionDto> DeleteFieldAsync(int fieldId);

    /// <summary>Проверяет значения карточки по описанию типа и возвращает их в нормализованном виде.</summary>
    Task<string> ValidateValuesAsync(int definitionId, Dictionary<string, JsonElement> values);
}

/// <summary>
/// Настраиваемые типы документов (GEN-06).
///
/// Тип описывается набором полей и шаблоном маршрута — то есть тем, что и так умеет
/// система, но без единой строки кода под конкретный тип. Так закрываются приказы,
/// корреспонденция и договоры вне закупок, которых в ТЗ нет поимённо.
/// </summary>
public class DocumentTypeDefinitionService : IDocumentTypeDefinitionService
{
    private static readonly Regex CodePattern = new("^[a-z][a-z0-9_]{1,63}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DelosferaDbContext _db;

    public DocumentTypeDefinitionService(DelosferaDbContext db) => _db = db;

    public async Task<List<DocumentTypeDefinitionDto>> ListAsync(bool includeInactive = false)
    {
        var query = _db.DocumentTypeDefinitions
            .Include(d => d.Fields)
            .Include(d => d.RouteTemplate)
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive) query = query.Where(d => d.IsActive);

        var rows = await query.OrderBy(d => d.TitleRu).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<DocumentTypeDefinitionDto> GetAsync(int id) => ToDto(await LoadAsync(id));

    public async Task<DocumentTypeDefinitionDto> CreateAsync(DocumentTypeSaveRequest request)
    {
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();

        if (!CodePattern.IsMatch(code))
            throw new InvalidOperationException(
                "Код типа: латиница в нижнем регистре, цифры и подчёркивание, начиная с буквы");

        if (string.IsNullOrWhiteSpace(request.TitleRu))
            throw new InvalidOperationException("Укажите название типа документа");

        if (await _db.DocumentTypeDefinitions.AnyAsync(d => d.Code == code))
            throw new InvalidOperationException($"Тип документа с кодом «{code}» уже есть");

        await EnsureTemplateAsync(request.RouteTemplateId);

        var definition = new DocumentTypeDefinition
        {
            Code = code,
            TitleRu = request.TitleRu.Trim(),
            TitleEn = request.TitleEn?.Trim(),
            TitleKg = request.TitleKg?.Trim(),
            Description = request.Description?.Trim(),
            RouteTemplateId = request.RouteTemplateId,
            NumberPattern = request.NumberPattern?.Trim(),
        };

        _db.DocumentTypeDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(definition.Id));
    }

    public async Task<DocumentTypeDefinitionDto> UpdateAsync(int id, DocumentTypeSaveRequest request)
    {
        var definition = await LoadTrackedAsync(id);
        await EnsureTemplateAsync(request.RouteTemplateId);

        if (!string.IsNullOrWhiteSpace(request.TitleRu)) definition.TitleRu = request.TitleRu.Trim();
        definition.TitleEn = request.TitleEn?.Trim();
        definition.TitleKg = request.TitleKg?.Trim();
        definition.Description = request.Description?.Trim();
        definition.RouteTemplateId = request.RouteTemplateId;
        definition.NumberPattern = request.NumberPattern?.Trim();
        if (request.IsActive is { } active) definition.IsActive = active;

        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(id));
    }

    public async Task DeleteAsync(int id)
    {
        var definition = await LoadTrackedAsync(id);

        // Тип с заведёнными карточками не удаляется: карточки останутся без описания
        // полей, и прочитать их значения станет нечем.
        var used = await _db.Documents.AnyAsync(d => d.DefinitionId == id);
        if (used)
            throw new InvalidOperationException(
                "По этому типу уже есть документы — выключите тип вместо удаления");

        _db.DocumentTypeDefinitions.Remove(definition);
        await _db.SaveChangesAsync();
    }

    public async Task<DocumentTypeDefinitionDto> AddFieldAsync(int definitionId, DocumentTypeFieldRequest request)
    {
        var definition = await LoadTrackedAsync(definitionId);
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();

        if (!CodePattern.IsMatch(code))
            throw new InvalidOperationException("Код поля: латиница, цифры и подчёркивание, начиная с буквы");

        if (definition.Fields.Any(f => f.Code == code))
            throw new InvalidOperationException($"Поле с кодом «{code}» в этом типе уже есть");

        if (string.IsNullOrWhiteSpace(request.TitleRu))
            throw new InvalidOperationException("Укажите название поля");

        await EnsureDictionaryAsync(request.Kind, request.DictionaryId);

        _db.DocumentTypeFields.Add(new DocumentTypeField
        {
            DefinitionId = definitionId,
            Code = code,
            TitleRu = request.TitleRu.Trim(),
            TitleEn = request.TitleEn?.Trim(),
            TitleKg = request.TitleKg?.Trim(),
            Kind = request.Kind,
            DictionaryId = request.DictionaryId,
            IsRequired = request.IsRequired,
            ShowInList = request.ShowInList,
            Hint = request.Hint?.Trim(),
            Order = request.Order ?? (definition.Fields.Count == 0 ? 1 : definition.Fields.Max(f => f.Order) + 1),
        });

        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(definitionId));
    }

    public async Task<DocumentTypeDefinitionDto> UpdateFieldAsync(int fieldId, DocumentTypeFieldRequest request)
    {
        var field = await _db.DocumentTypeFields.FirstOrDefaultAsync(f => f.Id == fieldId)
            ?? throw new KeyNotFoundException("Поле не найдено");

        await EnsureDictionaryAsync(request.Kind, request.DictionaryId);

        if (!string.IsNullOrWhiteSpace(request.TitleRu)) field.TitleRu = request.TitleRu.Trim();
        field.TitleEn = request.TitleEn?.Trim();
        field.TitleKg = request.TitleKg?.Trim();
        field.Hint = request.Hint?.Trim();
        field.IsRequired = request.IsRequired;
        field.ShowInList = request.ShowInList;
        if (request.Order is { } order) field.Order = order;

        // Вид поля и справочник менять нельзя: в карточках уже лежат значения прежнего
        // вида, и после смены их нечем прочитать. Нужен другой вид — заводится новое поле.
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(field.DefinitionId));
    }

    public async Task<DocumentTypeDefinitionDto> DeleteFieldAsync(int fieldId)
    {
        var field = await _db.DocumentTypeFields.FirstOrDefaultAsync(f => f.Id == fieldId)
            ?? throw new KeyNotFoundException("Поле не найдено");

        var definitionId = field.DefinitionId;

        _db.DocumentTypeFields.Remove(field);
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(definitionId));
    }

    public async Task<string> ValidateValuesAsync(int definitionId, Dictionary<string, JsonElement> values)
    {
        var definition = await LoadAsync(definitionId);
        var normalized = new Dictionary<string, object?>();

        foreach (var field in definition.Fields.OrderBy(f => f.Order))
        {
            values.TryGetValue(field.Code, out var raw);
            var empty = !values.ContainsKey(field.Code) || raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                        || (raw.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(raw.GetString()));

            if (empty)
            {
                if (field.IsRequired)
                    throw new InvalidOperationException($"Поле «{field.TitleRu}» обязательно к заполнению");

                continue;
            }

            normalized[field.Code] = await ConvertAsync(field, raw);
        }

        // Значения полей, которых нет в описании типа, не сохраняем: иначе карточка
        // копит мусор от прежних версий настроек, и непонятно, что из этого показывать.
        return JsonSerializer.Serialize(normalized, Json);
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<object?> ConvertAsync(DocumentTypeField field, JsonElement raw)
    {
        switch (field.Kind)
        {
            case CustomFieldKind.Number:
            case CustomFieldKind.Money:
                if (raw.ValueKind == JsonValueKind.Number) return raw.GetDecimal();
                if (decimal.TryParse(raw.ToString(), out var number)) return number;
                throw new InvalidOperationException($"Поле «{field.TitleRu}»: ожидается число");

            case CustomFieldKind.Checkbox:
                if (raw.ValueKind is JsonValueKind.True or JsonValueKind.False) return raw.GetBoolean();
                throw new InvalidOperationException($"Поле «{field.TitleRu}»: ожидается да или нет");

            case CustomFieldKind.Date:
                if (DateOnly.TryParse(raw.ToString(), out var date)) return date.ToString("yyyy-MM-dd");
                throw new InvalidOperationException($"Поле «{field.TitleRu}»: ожидается дата");

            case CustomFieldKind.Dictionary:
                var itemId = ParseId(field, raw);
                var exists = await _db.CustomDictionaryItems
                    .AnyAsync(i => i.Id == itemId && i.DictionaryId == field.DictionaryId);

                if (!exists)
                    throw new InvalidOperationException(
                        $"Поле «{field.TitleRu}»: значение не найдено в справочнике «{field.Dictionary?.TitleRu}»");

                return itemId;

            case CustomFieldKind.User:
                var userId = ParseId(field, raw);
                if (!await _db.Users.AnyAsync(u => u.Id == userId))
                    throw new InvalidOperationException($"Поле «{field.TitleRu}»: сотрудник не найден");

                return userId;

            case CustomFieldKind.OrgUnit:
                var unitId = ParseId(field, raw);
                if (!await _db.OrganizationUnits.AnyAsync(u => u.Id == unitId))
                    throw new InvalidOperationException($"Поле «{field.TitleRu}»: подразделение не найдено");

                return unitId;

            default:
                return raw.ValueKind == JsonValueKind.String ? raw.GetString()?.Trim() : raw.ToString();
        }
    }

    private static int ParseId(DocumentTypeField field, JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.Number) return raw.GetInt32();
        if (int.TryParse(raw.ToString(), out var id)) return id;

        throw new InvalidOperationException($"Поле «{field.TitleRu}»: ожидается ссылка на значение");
    }

    private async Task EnsureTemplateAsync(int? templateId)
    {
        if (templateId is null) return;

        if (!await _db.RouteTemplates.AnyAsync(t => t.Id == templateId))
            throw new KeyNotFoundException("Шаблон маршрута не найден");
    }

    private async Task EnsureDictionaryAsync(CustomFieldKind kind, int? dictionaryId)
    {
        if (kind != CustomFieldKind.Dictionary)
        {
            if (dictionaryId is not null)
                throw new InvalidOperationException("Справочник задаётся только полю вида «значение из справочника»");

            return;
        }

        if (dictionaryId is null)
            throw new InvalidOperationException("Для поля-справочника укажите, из какого справочника выбирать");

        if (!await _db.CustomDictionaries.AnyAsync(d => d.Id == dictionaryId))
            throw new KeyNotFoundException("Справочник не найден");
    }

    private async Task<DocumentTypeDefinition> LoadAsync(int id) =>
        await _db.DocumentTypeDefinitions
            .Include(d => d.Fields).ThenInclude(f => f.Dictionary)
            .Include(d => d.RouteTemplate)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Тип документа не найден");

    private async Task<DocumentTypeDefinition> LoadTrackedAsync(int id) =>
        await _db.DocumentTypeDefinitions.Include(d => d.Fields).FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Тип документа не найден");

    private static DocumentTypeDefinitionDto ToDto(DocumentTypeDefinition d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        TitleRu = d.TitleRu,
        TitleEn = d.TitleEn,
        TitleKg = d.TitleKg,
        Description = d.Description,
        RouteTemplateId = d.RouteTemplateId,
        RouteTemplateName = d.RouteTemplate?.Name,
        NumberPattern = d.NumberPattern,
        IsActive = d.IsActive,
        Fields = d.Fields
            .OrderBy(f => f.Order)
            .Select(f => new DocumentTypeFieldDto
            {
                Id = f.Id,
                Code = f.Code,
                TitleRu = f.TitleRu,
                TitleEn = f.TitleEn,
                TitleKg = f.TitleKg,
                Kind = f.Kind,
                KindTitle = KindTitle(f.Kind),
                DictionaryId = f.DictionaryId,
                DictionaryTitle = f.Dictionary?.TitleRu,
                IsRequired = f.IsRequired,
                ShowInList = f.ShowInList,
                Order = f.Order,
                Hint = f.Hint,
            })
            .ToList(),
    };

    private static string KindTitle(CustomFieldKind kind) => kind switch
    {
        CustomFieldKind.Text => "Строка",
        CustomFieldKind.MultilineText => "Текст",
        CustomFieldKind.Number => "Число",
        CustomFieldKind.Money => "Сумма",
        CustomFieldKind.Date => "Дата",
        CustomFieldKind.Checkbox => "Да/нет",
        CustomFieldKind.Dictionary => "Значение из справочника",
        CustomFieldKind.User => "Сотрудник",
        CustomFieldKind.OrgUnit => "Подразделение",
        _ => kind.ToString(),
    };
}
