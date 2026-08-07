using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public class RoleService : IRoleService
{
    private readonly DelosferaDbContext _db;

    public RoleService(DelosferaDbContext db)
    {
        _db = db;
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
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }
    
    public async Task<RoleResponse> GetByIdAsync(int id, string languageCode)
    {
        var entity = await _db.Roles.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        return ToResponse(entity, languageCode);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, string languageCode)
    {
        EnsureValidPermissionCodes(request.PermissionCodes);

        var entity = new Role
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            PermissionCodes = request.PermissionCodes.Distinct().ToArray()
        };

        _db.Roles.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<RoleResponse> UpdateAsync(int id, UpdateRoleRequest request, string languageCode)
    {
        var entity = await _db.Roles.FindAsync(id)
            ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        EnsureValidPermissionCodes(request.PermissionCodes);

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.PermissionCodes = request.PermissionCodes.Distinct().ToArray();
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Roles.FindAsync(id)
            ?? throw new KeyNotFoundException($"Роль с id={id} не найдена");

        _db.Roles.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить роль - она назначена одному или нескольким пользователям");
        }
    }

    private static void EnsureValidPermissionCodes(List<int> codes)
    {
        var validCodes = Enum.GetValues<PermissionCode>().Select(p => (int)p).ToHashSet();
        var invalid = codes.Where(c => !validCodes.Contains(c)).ToList();

        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Неизвестные коды прав: {string.Join(", ", invalid)}");
    }

    private static RoleResponse ToResponse(Role entity, string languageCode) => new()
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
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}