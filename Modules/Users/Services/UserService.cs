using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services.Authorization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;

using delosfera_server.Common.Security;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Users.Services;

public class UserService : IUserService
{
    private readonly DelosferaDbContext _db;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public UserService(
        DelosferaDbContext db,
        IUserPasswordHasher passwordHasher,
        IPasswordPolicy passwordPolicy,
        IAuditService audit,
        ICurrentUserService currentUser)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy;
        _audit = audit;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Хеширует пароль, предварительно проверив его по парольной политике (NFR-03).
    /// Проверка стоит здесь, а не в атрибутах DTO: политика настраивается службой ИБ,
    /// а атрибут — это константа в сборке.
    /// </summary>
    private string HashChecked(string password)
    {
        _passwordPolicy.Validate(password);
        return _passwordHasher.Hash(password);
    }

    /// <summary>
    /// Журнал действий администратора (NFR-03): заведение и изменение учётных записей,
    /// блокировка, смена ролей. Без него непонятно, кто и когда выдал доступ.
    /// </summary>
    private Task LogAdminAsync(int userId, string action, object? payload = null) =>
        _audit.LogAsync("User", userId, action, _currentUser.UserId == 0 ? null : _currentUser.UserId, payload);

    public async Task<List<UserResponse>> GetAllAsync(
        UserSortBy sortBy,
        string? search,
        List<int>? orgUnitIds,
        List<int>? positionIds,
        List<int>? roleIds,
        UserSource? source,
        bool? isBlocked,
        string languageCode)
    {
        IQueryable<User> query = _db.Users
            .Include(x => x.Position)
            .Include(x => x.OrgUnit)
            .Include(x => x.Roles)
            .Include(x => x.BlockedByUser);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.FullName, $"%{term}%") ||
                EF.Functions.ILike(x.Email, $"%{term}%"));
        }

        if (orgUnitIds is { Count: > 0 })
            query = query.Where(x => x.OrgUnitId.HasValue && orgUnitIds.Contains(x.OrgUnitId.Value));

        if (positionIds is { Count: > 0 })
            query = query.Where(x => x.PositionId.HasValue && positionIds.Contains(x.PositionId.Value));

        if (roleIds is { Count: > 0 })
            query = query.Where(x => x.Roles.Any(r => roleIds.Contains(r.Id)));

        if (source.HasValue)
            query = query.Where(x => x.Source == source.Value);

        // Работающей считается учётная запись, которую и не заблокировал администратор,
        // и которая активна сама по себе: отключённые в службе каталогов приходят
        // неактивными, и относить их к работающим значит показывать уволенных
        // наравне с действующими сотрудниками.
        if (isBlocked.HasValue)
            query = isBlocked.Value
                ? query.Where(x => x.BlockedAt != null || !x.IsActive)
                : query.Where(x => x.BlockedAt == null && x.IsActive);

        query = sortBy switch
        {
            UserSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            UserSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            UserSortBy.NameAsc => query.OrderBy(x => x.FullName),
            UserSortBy.NameDesc => query.OrderByDescending(x => x.FullName),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, string languageCode)
    {
        await EnsureEmailIsUniqueAsync(request.Email);

        if (request.PositionId.HasValue)
            await EnsurePositionExistsAsync(request.PositionId.Value);

        if (request.OrgUnitId.HasValue)
            await EnsureOrgUnitExistsAsync(request.OrgUnitId.Value);

        var roles = await GetRolesByIdsAsync(request.RoleIds);

        var entity = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashChecked(request.Password),
            PositionId = request.PositionId,
            OrgUnitId = request.OrgUnitId,
            Source = UserSource.Local, // через API всегда создаётся локальная УЗ; LDAP заводится синком
            Roles = roles
        };

        _db.Users.Add(entity);
        await _db.SaveChangesAsync();

        await LogAdminAsync(entity.Id, "UserCreated", new
        {
            email = entity.Email,
            roles = entity.Roles.Select(r => r.TitleRu).ToList(),
        });

        return await GetByIdAsync(entity.Id, languageCode);
    }

    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, string languageCode)
    {
        var entity = await _db.Users
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Пользователь с id={id} не найден");

        await EnsureEmailIsUniqueAsync(request.Email, excludeUserId: id);

        if (request.PositionId.HasValue)
            await EnsurePositionExistsAsync(request.PositionId.Value);

        if (request.OrgUnitId.HasValue)
            await EnsureOrgUnitExistsAsync(request.OrgUnitId.Value);

        entity.FullName = request.FullName;
        entity.Email = request.Email;
        entity.PositionId = request.PositionId;
        entity.OrgUnitId = request.OrgUnitId;
        entity.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
            entity.PasswordHash = HashChecked(request.Password);

        var rolesBefore = entity.Roles.Select(r => r.TitleRu).ToList();
        entity.Roles = await GetRolesByIdsAsync(request.RoleIds);

        await _db.SaveChangesAsync();

        // Смена набора ролей — это выдача или снятие доступа, и она должна быть видна
        // в журнале отдельно от прочих правок карточки.
        var rolesAfter = entity.Roles.Select(r => r.TitleRu).ToList();

        await LogAdminAsync(id, "UserUpdated", new
        {
            passwordChanged = !string.IsNullOrWhiteSpace(request.Password),
            rolesBefore,
            rolesAfter,
        });

        return await GetByIdAsync(id, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException($"Пользователь с id={id} не найден");

        _db.Users.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить пользователя — на него есть ссылки в других данных системы");
        }
    }

    public async Task<UserResponse> GetByIdAsync(int id, string languageCode)
    {
        var entity = await _db.Users
                         .Include(x => x.Position)
                         .Include(x => x.OrgUnit)
                         .Include(x => x.Roles)
                         .Include(x => x.BlockedByUser)
                         .FirstOrDefaultAsync(x => x.Id == id)
                     ?? throw new KeyNotFoundException($"Пользователь с id={id} не найден");

        return ToResponse(entity, languageCode);
    }

    public async Task<UserResponse> BlockAsync(int id, int blockedByUserId, string? reason, string languageCode)
    {
        var entity = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException($"Пользователь с id={id} не найден");

        entity.BlockedAt = DateTime.UtcNow;
        entity.BlockedByUserId = blockedByUserId;
        entity.BlockReason = reason;

        await _db.SaveChangesAsync();
        await LogAdminAsync(id, "UserBlocked", new {reason});

        return await GetByIdAsync(id, languageCode);
    }

    public async Task<UserResponse> UnblockAsync(int id, string languageCode)
    {
        var entity = await _db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException($"Пользователь с id={id} не найден");

        entity.BlockedAt = null;
        entity.BlockedByUserId = null;
        entity.BlockReason = null;

        // Разблокировка снимает и счётчик неудачных попыток: иначе сотрудник вернётся
        // к запертому входу с первой же опечатки.
        entity.FailedLoginAttempts = 0;
        entity.LockedUntil = null;

        await _db.SaveChangesAsync();
        await LogAdminAsync(id, "UserUnblocked");

        return await GetByIdAsync(id, languageCode);
    }

    private async Task EnsureEmailIsUniqueAsync(string email, int? excludeUserId = null)
    {
        var exists = await _db.Users.AnyAsync(x =>
            x.Email == email && (!excludeUserId.HasValue || x.Id != excludeUserId.Value));

        if (exists)
            throw new InvalidOperationException($"Пользователь с email={email} уже существует");
    }

    private async Task EnsurePositionExistsAsync(int positionId)
    {
        var exists = await _db.Positions.AnyAsync(x => x.Id == positionId);
        if (!exists)
            throw new KeyNotFoundException($"Должность с id={positionId} не найдена");
    }

    private async Task EnsureOrgUnitExistsAsync(int orgUnitId)
    {
        var exists = await _db.OrganizationUnits.AnyAsync(x => x.Id == orgUnitId);
        if (!exists)
            throw new KeyNotFoundException($"Структурное подразделение с id={orgUnitId} не найдено");
    }

    private async Task<List<Role>> GetRolesByIdsAsync(List<int> roleIds)
    {
        if (roleIds.Count == 0)
            return [];

        var roles = await _db.Roles.Where(x => roleIds.Contains(x.Id)).ToListAsync();

        var missing = roleIds.Except(roles.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Роли с id={string.Join(", ", missing)} не найдены");

        return roles;
    }

    private static UserResponse ToResponse(User entity, string languageCode) => new()
    {
        Id = entity.Id,
        FullName = entity.FullName,
        Email = entity.Email,
        Position = entity.Position is null ? null : new PositionResponse
        {
            Id = entity.Position.Id,
            Name = entity.Position.ResolveTitle(languageCode),
            TitleRu = entity.Position.TitleRu,
            TitleEn = entity.Position.TitleEn,
            TitleKg = entity.Position.TitleKg,
            CreatedAt = entity.Position.CreatedAt,
            UpdatedAt = entity.Position.UpdatedAt
        },
        OrgUnit = entity.OrgUnit is null ? null : new OrganizationUnitResponse
        {
            Id = entity.OrgUnit.Id,
            Name = entity.OrgUnit.ResolveTitle(languageCode),
            TitleRu = entity.OrgUnit.TitleRu,
            TitleEn = entity.OrgUnit.TitleEn,
            TitleKg = entity.OrgUnit.TitleKg,
            ParentId = entity.OrgUnit.ParentId,
            CreatedAt = entity.OrgUnit.CreatedAt,
            UpdatedAt = entity.OrgUnit.UpdatedAt
        },
        IsActive = entity.IsActive,
        LastLoginAt = entity.LastLoginAt,
        Source = entity.Source,
        BlockedAt = entity.BlockedAt,
        BlockedByUserName = entity.BlockedByUser?.FullName,
        BlockReason = entity.BlockReason,
        Roles = entity.Roles.Select(r => new Users.DTO.Response.RoleResponse
        {
            Id = r.Id,
            Name = r.ResolveTitle(languageCode),
            TitleRu = r.TitleRu,
            TitleEn = r.TitleEn,
            TitleKg = r.TitleKg,
            PermissionCodes = r.PermissionCodes.ToList(),
            Permissions = r.PermissionCodes
                .Where(code => Models.PermissionCatalog.Descriptions.ContainsKey((Models.PermissionCode)code))
                .Select(code => new Users.DTO.Response.PermissionResponse
                {
                    Code = code,
                    Key = ((Models.PermissionCode)code).ToString(),
                    Description = Models.PermissionCatalog.Descriptions[(Models.PermissionCode)code].ResolveTitle(languageCode)
                }).ToList(),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        }).ToList(),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}