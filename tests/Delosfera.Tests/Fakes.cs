using Microsoft.AspNetCore.Http;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Workflow.Services;
using delosfera_server.Modules.Workflow.Models;
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

/// <summary>
/// Уведомления в большинстве тестов не проверяем — пустая реализация.
///
/// Не sealed: тестам про круг адресатов нужно знать, кому ушло, и они
/// переопределяют CreateAsync. Дублировать ради этого все десять методов
/// интерфейса значило бы править две заглушки при каждом его изменении.
/// </summary>
internal class NoopNotificationService : INotificationService
{
    // CreateAsync — единственный метод, который дёргает тестируемый путь (уведомления при переходах).
    public virtual Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId) => Task.FromResult(0);
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

    public Task<FileAttachment> SaveGeneratedAsync(
        byte[] content, string fileName, string contentType, int userId,
        CancellationToken ct = default) => throw new NotImplementedException();

    public Task<string> ComputeHashAsync(IFormFile file, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);
}

/// <summary>
/// Лента событий. В тестах проверяются переходы состояний, а не то, что о них
/// написали в ленте, — записи копим в памяти на случай, если тест захочет
/// убедиться, что событие вообще произошло.
/// </summary>
internal sealed class FakeActivityLog : delosfera_server.Modules.ActivityLog.Services.IActivityLogService
{
    public List<(string Module, int EntityId, string EntityCode)> Entries { get; } = [];

    public void Log(
        string module,
        delosfera_server.Modules.ActivityLog.Models.ActivityEventKind kind,
        int entityId,
        string entityCode,
        int? actorUserId,
        delosfera_server.Modules.ActivityLog.Models.ActivityText text,
        string url) =>
        Entries.Add((module, entityId, entityCode));

    public Task<List<delosfera_server.Modules.ActivityLog.DTO.Response.ActivityLogEntryResponse>> GetRecentAsync(
        int limit, string languageCode, string? module = null) =>
        Task.FromResult(new List<delosfera_server.Modules.ActivityLog.DTO.Response.ActivityLogEntryResponse>());

    public Task<List<delosfera_server.Modules.ActivityLog.DTO.Response.ActivityLogEntryResponse>> GetByEntityAsync(
        string module, int entityId, string languageCode) =>
        Task.FromResult(new List<delosfera_server.Modules.ActivityLog.DTO.Response.ActivityLogEntryResponse>());
}

/// <summary>
/// Отпечаток версии карточки. Возвращает постоянное значение: тесты проверяют,
/// что подпись вообще привязана к версии, а не какова свёртка.
/// </summary>
internal sealed class FakeFingerprints : delosfera_server.Modules.Documents.Services.IDocumentFingerprintService
{
    public Task<string> ComputeAsync(int documentId, CancellationToken ct = default) =>
        Task.FromResult($"fingerprint-{documentId}");
}

/// <summary>
/// Подписание. Записывает, что и кем подписано, но криптографии не делает —
/// в тестах маршрутов проверяется факт вызова, а не содержимое подписи.
/// </summary>
internal sealed class FakeSignatures : delosfera_server.Modules.Signing.Services.ISignatureService
{
    public List<(int DocumentId, int UserId)> Signed { get; } = [];

    public Task<delosfera_server.Modules.Signing.Models.Signature> SignAsync(
        int documentAttachmentId,
        delosfera_server.Modules.Signing.Models.SignatureLevel level,
        int userId,
        string? stampMeta = null) =>
        Task.FromResult(new delosfera_server.Modules.Signing.Models.Signature
        {
            DocumentAttachmentId = documentAttachmentId,
            UserId = userId,
            Level = level,
            At = DateTime.UtcNow,
        });

    public Task<delosfera_server.Modules.Signing.Models.Signature> SignDocumentAsync(
        int documentId,
        delosfera_server.Modules.Signing.Models.SignatureLevel level,
        int userId,
        string? stampMeta = null)
    {
        Signed.Add((documentId, userId));

        return Task.FromResult(new delosfera_server.Modules.Signing.Models.Signature
        {
            DocumentId = documentId,
            UserId = userId,
            Level = level,
            At = DateTime.UtcNow,
        });
    }

    public Task RevokeForAttachmentAsync(int documentAttachmentId, string reason) => Task.CompletedTask;
}

/// <summary>
/// Регламент простой подписи. Возвращает «не требуется»: тесты проверяют
/// подписание как таковое, а согласие с регламентом — отдельный путь, у
/// которого свои проверки.
/// </summary>
internal sealed class NoRegulation : delosfera_server.Modules.Signing.Services.ISimpleSignatureRegulationService
{
    public Task<delosfera_server.Modules.Signing.Services.RegulationStateDto> GetStateAsync(
        int userId, CancellationToken ct = default) =>
        Task.FromResult(new delosfera_server.Modules.Signing.Services.RegulationStateDto
        {
            Required = false,
            Accepted = true,
        });

    public Task<delosfera_server.Modules.Signing.Services.RegulationStateDto> AcceptAsync(
        int userId, string version, CancellationToken ct = default) =>
        GetStateAsync(userId, ct);
}

/// <summary>
/// Очистка разметки. В тестах маршрутов текст записки не проверяется, поэтому
/// возвращаем как есть; сама очистка проверяется отдельными тестами
/// DocumentHtmlService.
/// </summary>
internal sealed class PassthroughHtml : IDocumentHtmlService
{
    public string? Sanitize(string? html) => html;
    public string? ToPlainText(string? html) => html;
}

/// <summary>Уведомления адресату здесь не проверяются — важен переход статуса.</summary>
internal sealed class SilentNotifications : INotificationService
{
    public Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId) =>
        Task.FromResult(0);

    public Task<PagedNotificationResponse> SearchAsync(
        NotificationFilterRequest request, int currentUserId, string languageCode) =>
        throw new NotSupportedException();

    public Task<NotificationResponse> GetByIdAsync(int id, int currentUserId, string languageCode) =>
        throw new NotSupportedException();

    public Task<NotificationResponse> MarkAsReadAsync(int id, int currentUserId, string languageCode) =>
        throw new NotSupportedException();

    public Task<NotificationResponse> MarkAsUnreadAsync(int id, int currentUserId, string languageCode) =>
        throw new NotSupportedException();

    public Task<int> MarkAllAsReadAsync(int currentUserId, NotificationCategory? category) =>
        throw new NotSupportedException();

    public Task<NotificationResponse> ToggleFavoriteAsync(int id, int currentUserId, string languageCode) =>
        throw new NotSupportedException();

    public Task DeleteForUserAsync(int id, int currentUserId) => throw new NotSupportedException();

    public Task<NotificationCountsResponse> GetCountsAsync(int currentUserId) =>
        throw new NotSupportedException();
}

/// <summary>Замещений нет: проверяется движок, а не подмена согласующего.</summary>
internal sealed class NoSubstitutions : ISubstitutionService
{
    public Task<List<SubstitutionDto>> ListAsync(int? userId) => Task.FromResult(new List<SubstitutionDto>());

    public Task<SubstitutionDto> CreateAsync(SubstitutionCreateRequest request, int actorUserId) =>
        throw new NotSupportedException();

    public Task<SubstitutionDto> CancelAsync(int id, int actorUserId) => throw new NotSupportedException();

    public Task<List<int>> GetActingForUserIdsAsync(int substituteUserId) =>
        Task.FromResult(new List<int>());
}

/// <summary>Уведомления в этих проверках не участвуют — важны переходы состояний.</summary>
internal sealed class SilentNotifier : IWorkflowNotifier
{
    public Task TaskAssignedAsync(IEnumerable<int> participantIds) => Task.CompletedTask;
    public Task OverdueAsync(int participantId, bool escalated) => Task.CompletedTask;

    public Task RouteFinishedAsync(int routeInstanceId, RouteInstanceStatus status, string? comment) =>
        Task.CompletedTask;
}
