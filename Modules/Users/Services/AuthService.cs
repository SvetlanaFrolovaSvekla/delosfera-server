using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Common.Security;
using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public class AuthService : IAuthService
{
    private readonly DelosferaDbContext _db;
    private readonly IUserPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILdapDirectory _directory;
    private readonly IPasswordPolicy _passwordPolicy;
    private readonly int _refreshTokenExpiryDays;

    public AuthService(
        DelosferaDbContext db,
        IUserPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILdapDirectory directory,
        IPasswordPolicy passwordPolicy,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _directory = directory;
        _passwordPolicy = passwordPolicy;
        _refreshTokenExpiryDays = int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "30");
    }

    /// <summary>SHA-256 (hex) от токена — в БД хранится только хеш.</summary>
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<AuthResult> LoginAsync(LoginRequest request, string languageCode)
    {
        var user = await LoadUserAsync(x => x.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Неверный email или пароль");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Учётная запись деактивирована");

        if (user.BlockedAt.HasValue)
            throw new UnauthorizedAccessException("Учётная запись заблокирована");

        // У доменной учётной записи локального пароля нет: он живёт в каталоге и
        // подчиняется доменным политикам. Иначе рядом с доменным появился бы второй
        // пароль, который не истекает и не блокируется вместе с учёткой.
        if (user.Source == UserSource.Ldap)
            throw new UnauthorizedAccessException(
                "Учётная запись доменная — используйте вход через службу каталогов");

        // Блокировка после серии неудачных попыток (NFR-03). Ограничение по адресу
        // уже стоит, но оно не спасает от подбора с разных адресов по одной учётке.
        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalMinutes));
            throw new UnauthorizedAccessException(
                $"Вход временно заблокирован после неудачных попыток. Повторите через {minutes} мин.");
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= _passwordPolicy.MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(_passwordPolicy.LockoutDuration);
                user.FailedLoginAttempts = 0;
            }

            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Неверный email или пароль");
        }

        // Успешный вход снимает счётчик: он про подбор пароля, а не про рассеянность.
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;

        var (accessToken, refreshToken) = await IssueNewTokenPairAsync(user);
        await _db.SaveChangesAsync();

        return new AuthResult(
            new LoginResponse { Token = accessToken, User = ToUserResponse(user, languageCode) },
            refreshToken);
    }

    public async Task<AuthResult> LoginWithDirectoryAsync(DomainLoginRequest request, string languageCode)
    {
        if (!_directory.Enabled)
            throw new UnauthorizedAccessException("Доменный вход не настроен");

        var entry = await _directory.AuthenticateAsync(request.Login, request.Password)
            ?? throw new UnauthorizedAccessException("Неверный доменный логин или пароль");

        if (entry.IsDisabled)
            throw new UnauthorizedAccessException("Доменная учётная запись отключена");

        if (string.IsNullOrWhiteSpace(entry.Email))
            throw new UnauthorizedAccessException(
                "В каталоге не заполнен адрес почты — обратитесь к администратору");

        // Каталог подтвердил личность, но прав в системе у сотрудника может не быть:
        // доступ выдаётся синхронизацией и ролями, а не самим фактом входа в домен.
        var user = await LoadUserAsync(x => x.Email == entry.Email)
            ?? throw new UnauthorizedAccessException(
                "Сотрудник не заведён в системе — требуется синхронизация каталога");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Учётная запись деактивирована");

        if (user.BlockedAt.HasValue)
            throw new UnauthorizedAccessException("Учётная запись заблокирована");

        user.LastLoginAt = DateTime.UtcNow;

        var (accessToken, refreshToken) = await IssueNewTokenPairAsync(user);
        await _db.SaveChangesAsync();

        return new AuthResult(
            new LoginResponse { Token = accessToken, User = ToUserResponse(user, languageCode) },
            refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string languageCode)
    {
        var refreshTokenHash = HashToken(refreshToken);

        var tokenEntity = await _db.Tokens
            .Include(x => x.User!).ThenInclude(u => u.Position)
            .Include(x => x.User!).ThenInclude(u => u.OrgUnit)
            .Include(x => x.User!).ThenInclude(u => u.Roles)
            .Include(x => x.User!).ThenInclude(u => u.BlockedByUser)
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHash)
            ?? throw new UnauthorizedAccessException("Недействительный refresh-токен");

        if (tokenEntity.IsLoggedOut)
            throw new UnauthorizedAccessException("Refresh-токен отозван");

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh-токен истёк");

        var user = tokenEntity.User!;

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Учётная запись деактивирована");

        // Пользователя могли заблокировать уже после выдачи refresh-токена —
        // проверяем блокировку и здесь, а не только при логине
        if (user.BlockedAt.HasValue)
            throw new UnauthorizedAccessException("Учётная запись заблокирована");

        var (accessToken, newRefreshToken) = await IssueNewTokenPairAsync(user);
        await _db.SaveChangesAsync();

        return new AuthResult(
            new LoginResponse { Token = accessToken, User = ToUserResponse(user, languageCode) },
            newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var refreshTokenHash = HashToken(refreshToken);
        var tokenEntity = await _db.Tokens.FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHash);
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
            RefreshTokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
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