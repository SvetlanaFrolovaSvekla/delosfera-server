using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Data;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.DTO.Response;
using delosfera_server.Modules.Notifications.Models;

namespace delosfera_server.Modules.Notifications.Services;

public class NotificationService : INotificationService
{
    private readonly DelosferaDbContext _db;

    public NotificationService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId)
    {
        List<int> recipientIds;

        if (request.ToAllUsers)
        {
            recipientIds = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        }
        else
        {
            if (request.UserIds.Count == 0)
                throw new InvalidOperationException("Не указаны получатели (UserIds пуст, ToAllUsers = false)");

            recipientIds = await _db.Users
                .Where(u => request.UserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            var missing = request.UserIds.Except(recipientIds).ToList();
            if (missing.Count > 0)
                throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");
        }

        var notification = new Notification
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            BodyRu = request.BodyRu,
            BodyEn = request.BodyEn,
            BodyKg = request.BodyKg,
            Category = request.Category,
            Severity = request.Severity,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Url = request.Url,
            CreatedByUserId = currentUserId,
            Recipients = recipientIds.Select(uid => new UserNotification
            {
                UserId = uid,
                IsRead = false,
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        return notification.Id;
    }

    public async Task<PagedNotificationResponse> SearchAsync(
        NotificationFilterRequest request, int currentUserId, string languageCode)
    {
        IQueryable<UserNotification> query = _db.UserNotifications
            .Include(x => x.Notification!).ThenInclude(n => n.CreatedByUser)
            .Where(x => x.UserId == currentUserId && !x.IsDeleted);

        if (request.Categories.Count > 0)
            query = query.Where(x => request.Categories.Contains(x.Notification!.Category));

        if (request.IsRead.HasValue)
            query = query.Where(x => x.IsRead == request.IsRead.Value);

        if (request.IsFavorite.HasValue)
            query = query.Where(x => x.IsFavorite == request.IsFavorite.Value);

        if (request.Severities.Count > 0)
            query = query.Where(x => request.Severities.Contains(x.Notification!.Severity));
        
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Notification!.TitleRu, $"%{term}%") ||
                EF.Functions.ILike(x.Notification!.BodyRu, $"%{term}%") ||
                (x.Notification.TitleEn != null && EF.Functions.ILike(x.Notification.TitleEn, $"%{term}%")) ||
                (x.Notification.TitleKg != null && EF.Functions.ILike(x.Notification.TitleKg, $"%{term}%")));
        }

        var totalCount = await query.CountAsync();

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedNotificationResponse
        {
            Items = items.Select(x => ToResponse(x, languageCode)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationResponse> GetByIdAsync(int userNotificationId, int currentUserId, string languageCode)
    {
        var entity = await LoadOwnedAsync(userNotificationId, currentUserId);
        return ToResponse(entity, languageCode);
    }

    public async Task<NotificationResponse> MarkAsReadAsync(int userNotificationId, int currentUserId, string languageCode)
    {
        var entity = await LoadOwnedAsync(userNotificationId, currentUserId);

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return ToResponse(entity, languageCode);
    }

    public async Task<NotificationResponse> MarkAsUnreadAsync(int userNotificationId, int currentUserId, string languageCode)
    {
        var entity = await LoadOwnedAsync(userNotificationId, currentUserId);

        entity.IsRead = false;
        entity.ReadAt = null;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<int> MarkAllAsReadAsync(int currentUserId, NotificationCategory? category)
    {
        var query = _db.UserNotifications
            .Where(x => x.UserId == currentUserId && !x.IsDeleted && !x.IsRead);

        if (category.HasValue)
            query = query.Where(x => x.Notification!.Category == category.Value);

        var now = DateTime.UtcNow;

        // ExecuteUpdateAsync - без загрузки сущностей в память, быстрее для массовой операции
        var affected = await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.IsRead, true)
            .SetProperty(x => x.ReadAt, now));

        return affected;
    }

    public async Task<NotificationResponse> ToggleFavoriteAsync(int userNotificationId, int currentUserId, string languageCode)
    {
        var entity = await LoadOwnedAsync(userNotificationId, currentUserId);

        entity.IsFavorite = !entity.IsFavorite;
        entity.FavoritedAt = entity.IsFavorite ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteForUserAsync(int userNotificationId, int currentUserId)
    {
        var entity = await LoadOwnedAsync(userNotificationId, currentUserId);

        entity.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<NotificationCountsResponse> GetCountsAsync(int currentUserId)
    {
        var baseQuery = _db.UserNotifications
            .Where(x => x.UserId == currentUserId && !x.IsDeleted);

        var totalUnread = await baseQuery.CountAsync(x => !x.IsRead);
        var totalFavorites = await baseQuery.CountAsync(x => x.IsFavorite);

        var byCategory = await baseQuery
            .Where(x => !x.IsRead)
            .GroupBy(x => x.Notification!.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        return new NotificationCountsResponse
        {
            TotalUnread = totalUnread,
            TotalFavorites = totalFavorites,
            UnreadByCategory = byCategory.ToDictionary(x => ((int)x.Category).ToString(), x => x.Count)
        };
    }

    private async Task<UserNotification> LoadOwnedAsync(int userNotificationId, int currentUserId)
    {
        var entity = await _db.UserNotifications
            .Include(x => x.Notification!).ThenInclude(n => n.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == userNotificationId && !x.IsDeleted)
            ?? throw new KeyNotFoundException($"Уведомление с id={userNotificationId} не найдено");

        if (entity.UserId != currentUserId)
            throw new UnauthorizedAccessException("Это уведомление адресовано другому пользователю");

        return entity;
    }

    private static NotificationResponse ToResponse(UserNotification x, string languageCode)
    {
        var n = x.Notification!;

        return new NotificationResponse
        {
            Id = x.Id,
            NotificationId = n.Id,
            Title = n.ResolveTitle(languageCode),
            Body = ResolveBody(n, languageCode),
            Category = n.Category.ToString(),
            Severity = n.Severity.ToString(),
            EntityType = n.EntityType,
            EntityId = n.EntityId,
            Url = n.Url,
            CreatedByUserId = n.CreatedByUserId,
            CreatedByName = n.CreatedByUser?.FullName,
            IsRead = x.IsRead,
            ReadAt = x.ReadAt,
            IsFavorite = x.IsFavorite,
            FavoritedAt = x.FavoritedAt,
            CreatedAt = x.CreatedAt
        };
    }

    private static string ResolveBody(Notification n, string languageCode) => languageCode switch
    {
        "en" => string.IsNullOrWhiteSpace(n.BodyEn) ? n.BodyRu : n.BodyEn,
        "kg" => string.IsNullOrWhiteSpace(n.BodyKg) ? n.BodyRu : n.BodyKg,
        _ => n.BodyRu
    };
}