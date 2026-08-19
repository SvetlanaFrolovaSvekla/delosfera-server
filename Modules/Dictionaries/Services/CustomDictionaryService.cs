using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface ICustomDictionaryService
{
    Task<List<CustomDictionaryDto>> ListAsync(bool includeInactive = false);
    Task<CustomDictionaryDto> GetAsync(int id);
    Task<CustomDictionaryDto> GetByCodeAsync(string code);
    Task<CustomDictionaryDto> CreateAsync(CustomDictionarySaveRequest request);
    Task<CustomDictionaryDto> UpdateAsync(int id, CustomDictionarySaveRequest request);
    Task DeleteAsync(int id);

    Task<CustomDictionaryDto> AddItemAsync(int dictionaryId, CustomDictionaryItemRequest request);
    Task<CustomDictionaryDto> UpdateItemAsync(int itemId, CustomDictionaryItemRequest request);
    Task<CustomDictionaryDto> DeleteItemAsync(int itemId);
}

/// <summary>
/// Справочники, заводимые администратором заказчика (GEN-07).
///
/// Удаление справочника и значения ограничено ссылками: карточки документов хранят
/// выбранное значение, и стереть его — значит оставить в карточке номер, который
/// больше ничего не означает. Ненужное выключается, а не удаляется.
/// </summary>
public class CustomDictionaryService : ICustomDictionaryService
{
    private static readonly Regex CodePattern = new("^[a-z][a-z0-9_]{1,63}$", RegexOptions.Compiled);

    private readonly DelosferaDbContext _db;

    public CustomDictionaryService(DelosferaDbContext db) => _db = db;

    public async Task<List<CustomDictionaryDto>> ListAsync(bool includeInactive = false)
    {
        var query = _db.CustomDictionaries.Include(d => d.Items).AsNoTracking().AsQueryable();

        if (!includeInactive) query = query.Where(d => d.IsActive);

        var rows = await query.OrderBy(d => d.TitleRu).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<CustomDictionaryDto> GetAsync(int id) => ToDto(await LoadAsync(id));

    public async Task<CustomDictionaryDto> GetByCodeAsync(string code)
    {
        var dictionary = await _db.CustomDictionaries
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code)
            ?? throw new KeyNotFoundException($"Справочник «{code}» не найден");

        return ToDto(dictionary);
    }

    public async Task<CustomDictionaryDto> CreateAsync(CustomDictionarySaveRequest request)
    {
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();

        if (!CodePattern.IsMatch(code))
            throw new InvalidOperationException(
                "Код справочника: латиница в нижнем регистре, цифры и подчёркивание, начиная с буквы");

        if (string.IsNullOrWhiteSpace(request.TitleRu))
            throw new InvalidOperationException("Укажите название справочника");

        if (await _db.CustomDictionaries.AnyAsync(d => d.Code == code))
            throw new InvalidOperationException($"Справочник с кодом «{code}» уже есть");

        var dictionary = new CustomDictionary
        {
            Code = code,
            TitleRu = request.TitleRu.Trim(),
            TitleEn = request.TitleEn?.Trim(),
            TitleKg = request.TitleKg?.Trim(),
            Description = request.Description?.Trim(),
            IsHierarchical = request.IsHierarchical,
        };

        _db.CustomDictionaries.Add(dictionary);
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(dictionary.Id));
    }

    public async Task<CustomDictionaryDto> UpdateAsync(int id, CustomDictionarySaveRequest request)
    {
        var dictionary = await LoadTrackedAsync(id);

        if (!string.IsNullOrWhiteSpace(request.TitleRu)) dictionary.TitleRu = request.TitleRu.Trim();
        dictionary.TitleEn = request.TitleEn?.Trim();
        dictionary.TitleKg = request.TitleKg?.Trim();
        dictionary.Description = request.Description?.Trim();
        dictionary.IsHierarchical = request.IsHierarchical;
        if (request.IsActive is { } active) dictionary.IsActive = active;

        // Код не меняется: на него ссылаются настройки типов документов и интеграции,
        // и переименование кода тихо порвало бы эти ссылки.
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(id));
    }

    public async Task DeleteAsync(int id)
    {
        var dictionary = await LoadTrackedAsync(id);

        var usedByField = await _db.DocumentTypeFields.AnyAsync(f => f.DictionaryId == id);
        if (usedByField)
            throw new InvalidOperationException(
                "Справочник используется полем типа документа — выключите его вместо удаления");

        _db.CustomDictionaries.Remove(dictionary);
        await _db.SaveChangesAsync();
    }

    public async Task<CustomDictionaryDto> AddItemAsync(int dictionaryId, CustomDictionaryItemRequest request)
    {
        var dictionary = await LoadTrackedAsync(dictionaryId);

        if (string.IsNullOrWhiteSpace(request.TitleRu))
            throw new InvalidOperationException("Укажите название значения");

        if (request.ParentId is { } parentId)
        {
            if (!dictionary.IsHierarchical)
                throw new InvalidOperationException("Справочник не иерархический — родитель не задаётся");

            if (!dictionary.Items.Any(i => i.Id == parentId))
                throw new InvalidOperationException("Родительское значение принадлежит другому справочнику");
        }

        _db.CustomDictionaryItems.Add(new CustomDictionaryItem
        {
            DictionaryId = dictionaryId,
            TitleRu = request.TitleRu.Trim(),
            TitleEn = request.TitleEn?.Trim(),
            TitleKg = request.TitleKg?.Trim(),
            Code = request.Code?.Trim(),
            ParentId = request.ParentId,
            Order = request.Order ?? (dictionary.Items.Count == 0 ? 1 : dictionary.Items.Max(i => i.Order) + 1),
        });

        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(dictionaryId));
    }

    public async Task<CustomDictionaryDto> UpdateItemAsync(int itemId, CustomDictionaryItemRequest request)
    {
        var item = await _db.CustomDictionaryItems.FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Значение справочника не найдено");

        if (!string.IsNullOrWhiteSpace(request.TitleRu)) item.TitleRu = request.TitleRu.Trim();
        item.TitleEn = request.TitleEn?.Trim();
        item.TitleKg = request.TitleKg?.Trim();
        item.Code = request.Code?.Trim();
        if (request.Order is { } order) item.Order = order;
        if (request.IsActive is { } active) item.IsActive = active;

        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(item.DictionaryId));
    }

    public async Task<CustomDictionaryDto> DeleteItemAsync(int itemId)
    {
        var item = await _db.CustomDictionaryItems.FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Значение справочника не найдено");

        var hasChildren = await _db.CustomDictionaryItems.AnyAsync(i => i.ParentId == itemId);
        if (hasChildren)
            throw new InvalidOperationException("У значения есть подчинённые — сначала уберите их");

        var dictionaryId = item.DictionaryId;

        _db.CustomDictionaryItems.Remove(item);
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(dictionaryId));
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<CustomDictionary> LoadAsync(int id) =>
        await _db.CustomDictionaries.Include(d => d.Items).AsNoTracking().FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Справочник не найден");

    private async Task<CustomDictionary> LoadTrackedAsync(int id) =>
        await _db.CustomDictionaries.Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Справочник не найден");

    private static CustomDictionaryDto ToDto(CustomDictionary d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        TitleRu = d.TitleRu,
        TitleEn = d.TitleEn,
        TitleKg = d.TitleKg,
        Description = d.Description,
        IsHierarchical = d.IsHierarchical,
        IsActive = d.IsActive,
        Items = d.Items
            .OrderBy(i => i.Order)
            .Select(i => new CustomDictionaryItemDto
            {
                Id = i.Id,
                TitleRu = i.TitleRu,
                TitleEn = i.TitleEn,
                TitleKg = i.TitleKg,
                Code = i.Code,
                ParentId = i.ParentId,
                Order = i.Order,
                IsActive = i.IsActive,
            })
            .ToList(),
    };
}
