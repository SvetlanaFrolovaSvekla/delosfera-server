using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzTemplateService
{
    Task<List<SzTemplateResponse>> ListAsync(int ownerUserId);
    Task<SzTemplateResponse> CreateAsync(SzTemplateSaveRequest request, int ownerUserId);
    Task<SzTemplateResponse> UpdateAsync(int id, SzTemplateSaveRequest request, int ownerUserId);
    Task DeleteAsync(int id, int ownerUserId);
}

/// <summary>
/// Личные шаблоны служебных записок (СЗ-6). Пресет полей хранится строкой JSON;
/// сервис только раскладывает его туда и обратно и следит, чтобы автор видел и правил
/// лишь свои шаблоны.
/// </summary>
public class SzTemplateService : ISzTemplateService
{
    private readonly DelosferaDbContext _db;

    // Пустые списки в пресете не пишем: шаблон без согласующих не должен раздувать
    // строку, а на клиенте пустой список и отсутствие поля значат одно и то же.
    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public SzTemplateService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<SzTemplateResponse>> ListAsync(int ownerUserId)
    {
        var rows = await _db.SzTemplates
            .Where(t => t.OwnerUserId == ownerUserId)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return rows.Select(Map).ToList();
    }

    public async Task<SzTemplateResponse> CreateAsync(SzTemplateSaveRequest request, int ownerUserId)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Укажите название шаблона");

        var now = DateTime.UtcNow;
        var entity = new SzTemplate
        {
            OwnerUserId = ownerUserId,
            Name = name,
            Payload = JsonSerializer.Serialize(request.Payload ?? new(), PayloadJson),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.SzTemplates.Add(entity);
        await _db.SaveChangesAsync();

        return Map(entity);
    }

    public async Task<SzTemplateResponse> UpdateAsync(int id, SzTemplateSaveRequest request, int ownerUserId)
    {
        var entity = await _db.SzTemplates.FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == ownerUserId)
            ?? throw new KeyNotFoundException("Шаблон не найден");

        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Укажите название шаблона");

        entity.Name = name;
        entity.Payload = JsonSerializer.Serialize(request.Payload ?? new(), PayloadJson);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(entity);
    }

    public async Task DeleteAsync(int id, int ownerUserId)
    {
        var entity = await _db.SzTemplates.FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == ownerUserId)
            ?? throw new KeyNotFoundException("Шаблон не найден");

        _db.SzTemplates.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private static SzTemplateResponse Map(SzTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Payload = DeserializePayload(t.Payload),
        UpdatedAt = t.UpdatedAt,
    };

    // Битый JSON старого шаблона не должен рушить весь список: вернём пустой пресет,
    // шаблон останется применимым как минимум по названию.
    private static SzTemplatePayload DeserializePayload(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<SzTemplatePayload>(payload, PayloadJson) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
