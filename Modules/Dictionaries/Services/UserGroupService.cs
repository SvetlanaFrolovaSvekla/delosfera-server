using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class UserGroupService : IUserGroupService
{
    private readonly DelosferaDbContext _db;

    public UserGroupService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<UserGroupResponse>> GetAllAsync(UserGroupSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<UserGroup> query = _db.UserGroups.Include(x => x.Users);

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
            UserGroupSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            UserGroupSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            UserGroupSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            UserGroupSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<UserGroupResponse> CreateAsync(CreateUserGroupRequest request, string languageCode)
    {
        var users = await GetUsersByIdsAsync(request.UserIds);

        var entity = new UserGroup
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            Users = users
        };

        _db.UserGroups.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<UserGroupResponse> UpdateAsync(int id, UpdateUserGroupRequest request, string languageCode)
    {
        var entity = await _db.UserGroups
            .Include(x => x.Users)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Группа пользователей с id={id} не найдена");

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.Users = await GetUsersByIdsAsync(request.UserIds);

        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.UserGroups.FindAsync(id)
            ?? throw new KeyNotFoundException($"Группа пользователей с id={id} не найдена");

        _db.UserGroups.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить группу пользователей — на неё есть ссылки в других данных системы");
        }
    }

    private async Task<List<User>> GetUsersByIdsAsync(List<int> userIds)
    {
        if (userIds.Count == 0)
            return [];

        var users = await _db.Users.Where(x => userIds.Contains(x.Id)).ToListAsync();

        var missing = userIds.Except(users.Select(u => u.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");

        return users;
    }

    private static UserGroupResponse ToResponse(UserGroup entity, string languageCode) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        Users = entity.Users.Select(u => new UserGroupMemberResponse
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email
        }).ToList(),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}