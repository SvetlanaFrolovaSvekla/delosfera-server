using Microsoft.AspNetCore.Http;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.DTO.Response;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace Delosfera.Tests;

/// <summary>Текущий пользователь с фиксированным id и набором прав (без HttpContext).</summary>
internal sealed class FakeCurrentUser(int userId, params PermissionCode[] permissions) : ICurrentUserService
{
    private readonly HashSet<PermissionCode> _permissions = [.. permissions];
    public int UserId { get; } = userId;
    public bool HasPermission(PermissionCode permission) => _permissions.Contains(permission);
}

/// <summary>Уведомления в тестах не проверяем — пустая реализация.</summary>
internal sealed class NoopNotificationService : INotificationService
{
    // CreateAsync — единственный метод, который дёргает тестируемый путь (уведомления при переходах).
    public Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId) => Task.FromResult(0);
    public Task DeleteForUserAsync(int id, int currentUserId) => Task.CompletedTask;
    public Task<int> MarkAllAsReadAsync(int currentUserId, NotificationCategory? category) => Task.FromResult(0);
    public Task<PagedNotificationResponse> SearchAsync(NotificationFilterRequest request, int currentUserId, string languageCode) => throw new NotImplementedException();
    public Task<NotificationResponse> GetByIdAsync(int id, int currentUserId, string languageCode) => throw new NotImplementedException();
    public Task<NotificationResponse> MarkAsReadAsync(int id, int currentUserId, string languageCode) => throw new NotImplementedException();
    public Task<NotificationResponse> MarkAsUnreadAsync(int id, int currentUserId, string languageCode) => throw new NotImplementedException();
    public Task<NotificationResponse> ToggleFavoriteAsync(int id, int currentUserId, string languageCode) => throw new NotImplementedException();
    public Task<NotificationCountsResponse> GetCountsAsync(int currentUserId) => throw new NotImplementedException();
}

/// <summary>Файловое хранилище не задействовано в тестируемых путях.</summary>
internal sealed class NoopFileStorage : IFileStorageService
{
    public Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int fileId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(int fileId, CancellationToken ct = default) => Task.CompletedTask;
}
