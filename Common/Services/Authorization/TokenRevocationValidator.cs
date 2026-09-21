using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using delosfera_server.Data;

namespace delosfera_server.Common.Services.Authorization;

/// <summary>
/// Мгновенный отзыв доступа (SEC-1).
///
/// Права и признак блокировки зашиты в JWT при входе, поэтому до сих пор увольнение или
/// понижение в правах действовали лишь после истечения access-токена — до 30 минут. Для
/// банка это дыра: уволенный сотрудник полчаса сохраняет доступ. Здесь на каждом
/// валидируемом токене пользователь сверяется с БД: заблокированному или деактивированному —
/// отказ (401), остальным набор прав берётся из ролей заново, взамен устаревших claim'ов
/// токена. Короткий кэш (10 с) не даёт бить базу на каждый запрос — задержка отзыва не
/// превышает этих секунд вместо прежних тридцати минут.
/// </summary>
public static class TokenRevocationValidator
{
    public sealed record Snapshot(bool Active, bool Blocked, int[] PermissionCodes);

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    public static async Task ValidateAsync(TokenValidatedContext ctx)
    {
        var idValue = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId))
        {
            ctx.Fail("Токен без идентификатора пользователя");
            return;
        }

        var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var snapshot = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var db = ctx.HttpContext.RequestServices.GetRequiredService<DelosferaDbContext>();
            var user = await db.Users.AsNoTracking()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return null;
            var perms = user.Roles.SelectMany(r => r.PermissionCodes).Distinct().ToArray();
            return new Snapshot(user.IsActive, user.BlockedAt != null, perms);
        });

        if (snapshot is null || !snapshot.Active || snapshot.Blocked)
        {
            ctx.Fail("Доступ отозван");
            return;
        }

        // Освежаем права из БД: claim'ы токена могли устареть после смены роли пользователя.
        if (ctx.Principal!.Identity is ClaimsIdentity identity)
        {
            foreach (var stale in identity.FindAll("permission").ToList())
                identity.RemoveClaim(stale);
            foreach (var code in snapshot.PermissionCodes)
                identity.AddClaim(new Claim("permission", code.ToString()));
        }
    }

    /// <summary>Сбросить кэш пользователя — чтобы блокировка/смена ролей подействовала сразу, не дожидаясь TTL.</summary>
    public static void Invalidate(IMemoryCache cache, int userId) => cache.Remove(CacheKey(userId));

    private static string CacheKey(int userId) => $"authz:user:{userId}";
}
