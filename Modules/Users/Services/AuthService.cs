using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public class AuthService : IAuthService
{
    private readonly DelosferaDbContext _db;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(DelosferaDbContext db, IUserPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string languageCode)
    {
        var user = await LoadUserAsync(x => x.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Неверный email или пароль");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Учётная запись деактивирована");

        if (user.BlockedAt.HasValue)
            throw new UnauthorizedAccessException("Учётная запись заблокирована");

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedAccessException("Неверный email или пароль");

        user.LastLoginAt = DateTime.UtcNow;

        var (accessToken, refreshToken) = await IssueNewTokenPairAsync(user);
        await _db.SaveChangesAsync();

        return new LoginResponse { Token = accessToken, RefreshToken = refreshToken, User = ToUserResponse(user, languageCode) };
    }

    public async Task<LoginResponse> RefreshAsync(RefreshTokenRequest request, string languageCode)
    {
        var tokenEntity = await _db.Tokens
            .Include(x => x.User!).ThenInclude(u => u.Position)
            .Include(x => x.User!).ThenInclude(u => u.OrgUnit)
            .Include(x => x.User!).ThenInclude(u => u.Roles)
            .Include(x => x.User!).ThenInclude(u => u.BlockedByUser)
            .FirstOrDefaultAsync(x => x.RefreshToken == request.RefreshToken)
            ?? throw new UnauthorizedAccessException("Недействительный refresh-токен");

        if (tokenEntity.IsLoggedOut)
            throw new UnauthorizedAccessException("Refresh-токен отозван");

        if (_jwtTokenService.IsExpired(tokenEntity.RefreshToken))
            throw new UnauthorizedAccessException("Refresh-токен истёк");

        var user = tokenEntity.User!;

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Учётная запись деактивирована");

        // Пользователя могли заблокировать уже после выдачи refresh-токена —
        // проверяем блокировку и здесь, а не только при логине
        if (user.BlockedAt.HasValue)
            throw new UnauthorizedAccessException("Учётная запись заблокирована");

        var (accessToken, refreshToken) = await IssueNewTokenPairAsync(user);
        await _db.SaveChangesAsync();

        return new LoginResponse { Token = accessToken, RefreshToken = refreshToken, User = ToUserResponse(user, languageCode) };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenEntity = await _db.Tokens.FirstOrDefaultAsync(x => x.RefreshToken == refreshToken);
        if (tokenEntity is null) return;

        tokenEntity.IsLoggedOut = true;
        await _db.SaveChangesAsync();
    }

    // Отзывает все активные токены пользователя и выдаёт новую пару
    private async Task<(string AccessToken, string RefreshToken)> IssueNewTokenPairAsync(User user)
    {
        var oldTokens = await _db.Tokens.Where(x => x.UserId == user.Id && !x.IsLoggedOut).ToListAsync();
        foreach (var old in oldTokens)
            old.IsLoggedOut = true;

        var permissionCodes = user.Roles.SelectMany(r => r.PermissionCodes).Distinct().ToList();
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissionCodes);
        var refreshToken = _jwtTokenService.GenerateRefreshToken(user);

        _db.Tokens.Add(new Token
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IsLoggedOut = false,
            UserId = user.Id
        });

        return (accessToken, refreshToken);
    }

    private Task<User?> LoadUserAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate) =>
        _db.Users
            .Include(x => x.Position)
            .Include(x => x.OrgUnit)
            .Include(x => x.Roles)
            .Include(x => x.BlockedByUser)
            .FirstOrDefaultAsync(predicate);

    private static UserResponse ToUserResponse(User entity, string languageCode) => new()
    {
        Id = entity.Id,
        FullName = entity.FullName,
        Email = entity.Email,
        Position = entity.Position is null ? null : new PositionResponse
        {
            Id = entity.Position.Id, Name = entity.Position.ResolveTitle(languageCode),
            TitleRu = entity.Position.TitleRu, TitleEn = entity.Position.TitleEn, TitleKg = entity.Position.TitleKg,
            CreatedAt = entity.Position.CreatedAt, UpdatedAt = entity.Position.UpdatedAt
        },
        OrgUnit = entity.OrgUnit is null ? null : new OrganizationUnitResponse
        {
            Id = entity.OrgUnit.Id, Name = entity.OrgUnit.ResolveTitle(languageCode),
            TitleRu = entity.OrgUnit.TitleRu, TitleEn = entity.OrgUnit.TitleEn, TitleKg = entity.OrgUnit.TitleKg,
            ParentId = entity.OrgUnit.ParentId, CreatedAt = entity.OrgUnit.CreatedAt, UpdatedAt = entity.OrgUnit.UpdatedAt
        },
        IsActive = entity.IsActive,
        LastLoginAt = entity.LastLoginAt,
        Source = entity.Source,
        BlockedAt = entity.BlockedAt,
        BlockedByUserName = entity.BlockedByUser?.FullName,
        BlockReason = entity.BlockReason,
        Roles = entity.Roles.Select(r => new Users.DTO.Response.RoleResponse
        {
            Id = r.Id, Name = r.ResolveTitle(languageCode),
            TitleRu = r.TitleRu, TitleEn = r.TitleEn, TitleKg = r.TitleKg,
            PermissionCodes = r.PermissionCodes.ToList(),
            Permissions = r.PermissionCodes
                .Where(code => Models.PermissionCatalog.Descriptions.ContainsKey((Models.PermissionCode)code))
                .Select(code => new Users.DTO.Response.PermissionResponse
                {
                    Code = code, Key = ((Models.PermissionCode)code).ToString(),
                    Description = Models.PermissionCatalog.Descriptions[(Models.PermissionCode)code].ResolveTitle(languageCode)
                }).ToList(),
            CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
        }).ToList(),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}