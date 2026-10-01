using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;

using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Users.Services;

public class RoleService : IRoleService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public RoleService(DelosferaDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public Task<List<PermissionResponse>> GetAllPermissionsAsync(string languageCode) =>
        Task.FromResult(PermissionCatalog.GetAll(languageCode));

    public async Task<List<RoleResponse>> GetAllAsync(RoleSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<Role> query = _db.Roles;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleRu, $"%{term}%") ||
                (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{term}%")) ||
                (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{term}%")));
        }

        query = sortBy switch
        {
            RoleSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            RoleSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            RoleSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            RoleSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        var counts = await UsersCountByRoleAsync();
        return entities.Select(x => ToResponse(x, languageCode, counts.GetValueOrDefault(x.Id))).ToList();
    }
    
    public async Task<RoleResponse> GetByIdAsync(int id, string languageCode)
    {
        var entity = await _db.Roles.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        var counts = await UsersCountByRoleAsync();
        return ToResponse(entity, languageCode, counts.GetValueOrDefault(id));
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, string languageCode)
    {
        EnsureValidPermissionCodes(request.PermissionCodes);
        var titleRu = NormalizeTitle(request.TitleRu);
        await EnsureTitleIsFreeAsync(titleRu, null);

        var entity = new Role
        {
            TitleRu = titleRu,
            TitleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim(),
            TitleKg = string.IsNullOrWhiteSpace(request.TitleKg) ? null : request.TitleKg.Trim(),
            PermissionCodes = request.PermissionCodes.Distinct().ToArray()
        };

        _db.Roles.Add(entity);
        await _db.SaveChangesAsync();

        await LogAdminAsync(entity.Id, "RoleCreated", new
        {
            title = entity.TitleRu,
            permissions = entity.PermissionCodes,
        });

        return ToResponse(entity, languageCode);
    }

    public async Task<RoleResponse> UpdateAsync(int id, UpdateRoleRequest request, string languageCode)
    {
        var entity = await _db.Roles.FindAsync(id)
            ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        EnsureValidPermissionCodes(request.PermissionCodes);

        var titleRu = NormalizeTitle(request.TitleRu);
        var titleBefore = entity.TitleRu;
        var renamed = !string.Equals(titleRu, entity.TitleRu, StringComparison.Ordinal);
        if (renamed)
        {
            // На название системной роли завязаны процессы (раздача прав по умолчанию,
            // роль для синхронизации с каталогом, органы банка) — переименование их сломает.
            if (CoreRolesSeeder.IsSystem(entity))
                throw new InvalidOperationException(
                    "Системную роль нельзя переименовать — меняйте только её права");
            await EnsureTitleIsFreeAsync(titleRu, entity.Id);
        }

        // Состав прав до правки — главное, что нужно в разборе «откуда у сотрудника
        // этот доступ»: сама по себе новая версия роли на вопрос не отвечает.
        var permissionsBefore = entity.PermissionCodes.ToArray();

        entity.TitleRu = titleRu;
        entity.TitleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();
        entity.TitleKg = string.IsNullOrWhiteSpace(request.TitleKg) ? null : request.TitleKg.Trim();
        entity.PermissionCodes = request.PermissionCodes.Distinct().ToArray();
        await _db.SaveChangesAsync();

        if (renamed)
            await LogAdminAsync(entity.Id, "RoleRenamed", new {before = titleBefore, after = entity.TitleRu});

        await LogAdminAsync(entity.Id, "RolePermissionsChanged", new
        {
            title = entity.TitleRu,
            before = permissionsBefore,
            after = entity.PermissionCodes,
            granted = entity.PermissionCodes.Except(permissionsBefore).ToArray(),
            revoked = permissionsBefore.Except(entity.PermissionCodes).ToArray(),
        });

        var counts = await UsersCountByRoleAsync();
        return ToResponse(entity, languageCode, counts.GetValueOrDefault(entity.Id));
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Roles.FindAsync(id)
            ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        if (CoreRolesSeeder.IsSystem(entity))
            throw new InvalidOperationException(
                $"Роль «{entity.TitleRu}» системная — её нельзя удалить");

        // Роль снимается со всех, у кого она есть, и только потом удаляется: иначе
        // связь пользователь—роль не даёт удалить запись, а держатели роли
        // остались бы с правами несуществующей роли в выданных токенах до перевхода.
        var holders = await _db.Users
            .Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.Id == id))
            .ToListAsync();

        foreach (var user in holders)
            user.Roles.Remove(user.Roles.First(r => r.Id == id));

        _db.Roles.Remove(entity);
        var removedTitle = entity.TitleRu;

        try
        {
            await _db.SaveChangesAsync();
            await LogAdminAsync(id, "RoleDeleted", new
            {
                title = removedTitle,
                revokedFromUserIds = holders.Select(u => u.Id).ToArray(),
            });
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Не удалось удалить роль — на неё ссылаются другие записи");
        }
    }

    /// <summary>Журнал действий администратора по ролям (NFR-03).</summary>
    private Task LogAdminAsync(int roleId, string action, object? payload = null) =>
        _audit.LogAsync("Role", roleId, action, _currentUser.UserId == 0 ? null : _currentUser.UserId, payload);

    private static void EnsureValidPermissionCodes(List<int> codes)
    {
        var validCodes = Enum.GetValues<PermissionCode>().Select(p => (int)p).ToHashSet();
        var invalid = codes.Where(c => !validCodes.Contains(c)).ToList();

        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Неизвестные коды прав: {string.Join(", ", invalid)}");
    }

    /// <summary>Сколько пользователей у каждой роли — одним запросом на все роли.</summary>
    private async Task<Dictionary<int, int>> UsersCountByRoleAsync() =>
        await _db.Users
            .SelectMany(u => u.Roles.Select(r => r.Id))
            .GroupBy(roleId => roleId)
            .Select(g => new {RoleId = g.Key, Count = g.Count()})
            .ToDictionaryAsync(x => x.RoleId, x => x.Count);

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Название роли не может быть пустым");
        return trimmed;
    }

    private async Task EnsureTitleIsFreeAsync(string titleRu, int? exceptId)
    {
        var lower = titleRu.ToLower();
        var taken = await _db.Roles.AnyAsync(r =>
            r.TitleRu.ToLower() == lower && (exceptId == null || r.Id != exceptId));
        if (taken)
            throw new InvalidOperationException($"Роль «{titleRu}» уже существует");
    }

    private static RoleResponse ToResponse(Role entity, string languageCode, int usersCount = 0) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        PermissionCodes = entity.PermissionCodes.ToList(),
        Permissions = entity.PermissionCodes
            .Where(code => PermissionCatalog.Descriptions.ContainsKey((PermissionCode)code))
            .Select(code => new PermissionResponse
            {
                Code = code,
                Key = ((PermissionCode)code).ToString(),
                Description = PermissionCatalog.Descriptions[(PermissionCode)code].ResolveTitle(languageCode)
            })
            .ToList(),
        IsSystem = CoreRolesSeeder.IsSystem(entity),
        UsersCount = usersCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}