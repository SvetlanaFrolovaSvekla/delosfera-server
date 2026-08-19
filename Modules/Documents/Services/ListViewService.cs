using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Documents.Services;

public interface IListViewService
{
    Task<List<ListViewDto>> ListAsync(string scope, int userId);
    Task<ListViewDto> SaveAsync(ListViewSaveRequest request, int userId);
    Task DeleteAsync(int id, int userId);
}

/// <summary>
/// Настраиваемые представления журналов (GEN-10).
///
/// Личное представление принадлежит сотруднику, общее заводит администратор для всех.
/// Разделение важно: делопроизводителю и куратору нужны разные колонки одного журнала,
/// и попытка договориться об одном наборе заканчивается тем, что неудобно обоим.
/// </summary>
public class ListViewService : IListViewService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListViewService(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<ListViewDto>> ListAsync(string scope, int userId)
    {
        var rows = await _db.ListViews
            .Where(v => v.Scope == scope && (v.UserId == null || v.UserId == userId))
            .OrderBy(v => v.UserId == null ? 0 : 1)
            .ThenBy(v => v.Name)
            .AsNoTracking()
            .ToListAsync();

        var canManageShared = _currentUser.HasPermission(PermissionCode.ManageGeneralDictionaries);

        return rows.Select(v => ToDto(v, userId, canManageShared)).ToList();
    }

    public async Task<ListViewDto> SaveAsync(ListViewSaveRequest request, int userId)
    {
        if (string.IsNullOrWhiteSpace(request.Scope))
            throw new InvalidOperationException("Не указан журнал, к которому относится представление");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Укажите название представления");

        if (request.Columns.Count == 0)
            throw new InvalidOperationException("Выберите хотя бы одну колонку");

        var canManageShared = _currentUser.HasPermission(PermissionCode.ManageGeneralDictionaries);

        if (request.IsShared && !canManageShared)
            throw new UnauthorizedAccessException(
                "Общее представление заводит администратор — своё сохраняется как личное");

        var ownerId = request.IsShared ? (int?)null : userId;
        var name = request.Name.Trim();

        var existing = await _db.ListViews
            .FirstOrDefaultAsync(v => v.Scope == request.Scope && v.Name == name && v.UserId == ownerId);

        // Сохранение под тем же названием — правка своего набора колонок, а не ошибка:
        // представление настраивают итеративно.
        if (existing is null)
        {
            existing = new ListView
            {
                Scope = request.Scope.Trim(),
                Name = name,
                UserId = ownerId,
                Columns = "[]",
            };

            _db.ListViews.Add(existing);
        }

        existing.Columns = JsonSerializer.Serialize(request.Columns, Json);
        existing.Filter = request.Filter is { } filter ? filter.GetRawText() : null;

        if (request.IsDefault) await ClearDefaultAsync(request.Scope, ownerId);
        existing.IsDefault = request.IsDefault;

        await _db.SaveChangesAsync();

        return ToDto(existing, userId, canManageShared);
    }

    public async Task DeleteAsync(int id, int userId)
    {
        var view = await _db.ListViews.FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new KeyNotFoundException("Представление не найдено");

        if (view.UserId is null && !_currentUser.HasPermission(PermissionCode.ManageGeneralDictionaries))
            throw new UnauthorizedAccessException("Общее представление удаляет администратор");

        if (view.UserId is { } owner && owner != userId)
            throw new UnauthorizedAccessException("Это личное представление другого сотрудника");

        _db.ListViews.Remove(view);
        await _db.SaveChangesAsync();
    }

    private async Task ClearDefaultAsync(string scope, int? ownerId)
    {
        var others = await _db.ListViews
            .Where(v => v.Scope == scope && v.UserId == ownerId && v.IsDefault)
            .ToListAsync();

        foreach (var view in others) view.IsDefault = false;
    }

    private static ListViewDto ToDto(ListView v, int userId, bool canManageShared) => new()
    {
        Id = v.Id,
        Scope = v.Scope,
        Name = v.Name,
        Columns = JsonSerializer.Deserialize<List<string>>(v.Columns, Json) ?? [],
        Filter = v.Filter is null ? null : JsonSerializer.Deserialize<JsonElement>(v.Filter, Json),
        IsShared = v.UserId is null,
        IsDefault = v.IsDefault,
        CanEdit = v.UserId is null ? canManageShared : v.UserId == userId,
    };
}
