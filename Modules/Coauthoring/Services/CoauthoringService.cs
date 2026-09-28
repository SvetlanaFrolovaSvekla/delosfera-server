using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Coauthoring.DTO;
using delosfera_server.Modules.Coauthoring.Options;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Coauthoring.Services;

public interface ICoauthoringService
{
    /// <summary>Собрать подписанный конфиг редактора OnlyOffice для вложения карточки.</summary>
    Task<OnlyOfficeConfigResponse> BuildConfigAsync(int attachmentId, string apiBaseUrl, CancellationToken ct = default);

    /// <summary>Отдать текущее содержимое вложения Document Server'у по подписанной ссылке.</summary>
    Task<(Stream Stream, string ContentType, string FileName)> OpenForDownloadAsync(
        int attachmentId, string token, CancellationToken ct = default);

    /// <summary>Обработать callback DS: на status=2/6 сохранить правки новой версией.</summary>
    Task HandleCallbackAsync(int attachmentId, OnlyOfficeCallbackRequest body, CancellationToken ct = default);
}

/// <summary>
/// Онлайн-соредактирование вложений через OnlyOffice Document Server (COOP-1).
///
/// Поток: клиент запрашивает <see cref="BuildConfigAsync"/> → получает подписанный
/// конфиг и грузит редактор; DS забирает файл по подписанной ссылке
/// (<see cref="OpenForDownloadAsync"/>); по завершении правок DS шлёт callback
/// (<see cref="HandleCallbackAsync"/>), и результат ложится НОВОЙ версией в MinIO,
/// не перезатирая прежнюю (таблица attachment_version), с фиксацией в аудит.
///
/// Права: открыть файл может лишь тот, кому он доступен (<see cref="IFileAccessAuthorizer"/>);
/// правку (edit) получает автор карточки либо администратор системы, остальным —
/// рецензирование и комментарии. Изменение содержимого аннулирует подписи под
/// вложением (SIG-01), как и обычная замена файла.
/// </summary>
public sealed class CoauthoringService : ICoauthoringService
{
    private static readonly string[] EditableExtensions = [".docx", ".xlsx", ".pptx"];

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IAttachmentVersionStorage _versionStorage;
    private readonly IOnlyOfficeDocumentFetcher _fetcher;
    private readonly IFileAccessAuthorizer _accessAuthorizer;
    private readonly ICurrentUserService _currentUser;
    private readonly ISignatureService _signatures;
    private readonly IAuditService _audit;
    private readonly OnlyOfficeOptions _options;
    private readonly ILogger<CoauthoringService> _logger;

    public CoauthoringService(
        DelosferaDbContext db,
        IFileStorageService files,
        IAttachmentVersionStorage versionStorage,
        IOnlyOfficeDocumentFetcher fetcher,
        IFileAccessAuthorizer accessAuthorizer,
        ICurrentUserService currentUser,
        ISignatureService signatures,
        IAuditService audit,
        IOptions<OnlyOfficeOptions> options,
        ILogger<CoauthoringService> logger)
    {
        _db = db;
        _files = files;
        _versionStorage = versionStorage;
        _fetcher = fetcher;
        _accessAuthorizer = accessAuthorizer;
        _currentUser = currentUser;
        _signatures = signatures;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OnlyOfficeConfigResponse> BuildConfigAsync(
        int attachmentId, string apiBaseUrl, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException(
                "Интеграция с OnlyOffice не настроена (OnlyOffice:ServerUrl / OnlyOffice:JwtSecret).");

        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        if (!int.TryParse(attachment.FileRef, out var fileId))
            throw new InvalidOperationException("Ссылка на файл в хранилище повреждена");

        // Доступ к самому файлу — по общим правилам (закрывает IDOR): открыть на
        // соредактирование можно лишь то, что пользователю и так видно.
        if (!await _accessAuthorizer.CanCurrentUserAccessAsync(fileId, ct))
            throw new UnauthorizedAccessException("Нет доступа к файлу");

        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        if (!EditableExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"Формат {ext} не поддерживает онлайн-соредактирование (только docx/xlsx/pptx)");

        var fileAttachment = await _db.FileAttachments.FindAsync([fileId], ct)
            ?? throw new KeyNotFoundException("Файл не найден в хранилище");

        var authorId = await _db.Documents
            .Where(d => d.Id == attachment.DocumentId)
            .Select(d => (int?)d.AuthorId)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Документ не найден");

        // Правку получает автор карточки или администратор системы — те же, кто вправе
        // заменить файл (DocumentAttachmentService.EnsureAuthorOrPrivileged). Остальным,
        // кто файл видит, даём рецензирование и комментарии.
        var canEdit = authorId == _currentUser.UserId
                      || _currentUser.HasPermission(PermissionCode.ManageSystemSettings);

        var latestVersion = await _db.AttachmentVersions
            .Where(v => v.FileAttachmentId == fileId)
            .Select(v => (int?)v.Version)
            .MaxAsync(ct) ?? 0;

        var publicBase = string.IsNullOrWhiteSpace(_options.ApiPublicUrl)
            ? apiBaseUrl.TrimEnd('/')
            : _options.ApiPublicUrl.TrimEnd('/');

        // Ключ = версия + свёртка содержимого: меняется при каждой новой версии, и DS
        // перечитывает файл вместо показа закэшированной прежней редакции.
        var hashPart = (fileAttachment.Hash ?? Guid.NewGuid().ToString("N"))
            .Replace("-", "");
        var key = $"att{fileId}v{latestVersion}{hashPart}";
        if (key.Length > 128) key = key[..128];

        var downloadToken = OnlyOfficeJwt.Sign(new
        {
            attachmentId,
            fileId,
            purpose = "download",
            exp = DateTimeOffset.UtcNow.AddSeconds(_options.DocumentUrlTtlSeconds).ToUnixTimeSeconds(),
        }, _options.JwtSecret);

        var documentUrl = $"{publicBase}/api/coauthoring/download/{attachmentId}?token={Uri.EscapeDataString(downloadToken)}";
        var callbackUrl = $"{publicBase}/api/coauthoring/callback?attachmentId={attachmentId}";

        var response = new OnlyOfficeConfigResponse
        {
            ServerUrl = _options.ServerUrl.TrimEnd('/'),
            DocumentType = DocumentTypeFor(ext),
            Document = new OnlyOfficeDocument
            {
                FileType = ext.TrimStart('.'),
                Key = key,
                Title = attachment.FileName,
                Url = documentUrl,
                Permissions = new OnlyOfficePermissions
                {
                    Edit = canEdit,
                    Review = true,
                    Comment = true,
                },
            },
            EditorConfig = new OnlyOfficeEditorConfig
            {
                Mode = canEdit ? "edit" : "review",
                CallbackUrl = callbackUrl,
                User = new OnlyOfficeUser
                {
                    Id = _currentUser.UserId.ToString(),
                    Name = await _db.Users
                        .Where(u => u.Id == _currentUser.UserId)
                        .Select(u => u.FullName)
                        .FirstOrDefaultAsync(ct) ?? _currentUser.UserId.ToString(),
                },
            },
            Token = string.Empty,
        };

        // Подпись всего конфига — DS сверит её перед открытием документа.
        response.Token = OnlyOfficeJwt.Sign(new
        {
            document = response.Document,
            editorConfig = response.EditorConfig,
            documentType = response.DocumentType,
        }, _options.JwtSecret);

        await _audit.LogAsync("CoauthoringSession", attachmentId, "ConfigIssued", _currentUser.UserId,
            new { fileId, canEdit, key });

        return response;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> OpenForDownloadAsync(
        int attachmentId, string token, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException("Интеграция с OnlyOffice не настроена.");

        if (!OnlyOfficeJwt.TryValidate(token, _options.JwtSecret, out var payload)
            || !payload.TryGetProperty("attachmentId", out var att)
            || att.GetInt32() != attachmentId)
            throw new UnauthorizedAccessException("Недействительная ссылка на файл");

        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        if (!int.TryParse(attachment.FileRef, out var fileId))
            throw new InvalidOperationException("Ссылка на файл в хранилище повреждена");

        return await _files.DownloadAsync(fileId, ct);
    }

    public async Task HandleCallbackAsync(
        int attachmentId, OnlyOfficeCallbackRequest body, CancellationToken ct = default)
    {
        // JWT-валидация тела: когда DS настроен с секретом, callback приходит с токеном,
        // и без валидной подписи ему доверять нельзя (иначе кто угодно подменит версию).
        if (_options.IsConfigured)
        {
            if (string.IsNullOrWhiteSpace(body.Token)
                || !OnlyOfficeJwt.TryValidate(body.Token, _options.JwtSecret, out _))
                throw new UnauthorizedAccessException("Callback без действительной подписи");
        }

        // Значимо для сохранения только 2 (готов) и 6 (force-save). Остальные статусы —
        // «редактируется» / «закрыт без изменений» / ошибки — ничего не сохраняют.
        if (body.Status != 2 && body.Status != 6)
            return;

        if (string.IsNullOrWhiteSpace(body.Url))
            throw new InvalidOperationException("Callback status=2/6 без ссылки на файл");

        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        if (!int.TryParse(attachment.FileRef, out var fileId))
            throw new InvalidOperationException("Ссылка на файл в хранилище повреждена");

        var fileAttachment = await _db.FileAttachments.FindAsync([fileId], ct)
            ?? throw new KeyNotFoundException("Файл не найден в хранилище");

        var bytes = await _fetcher.FetchAsync(body.Url!, ct);
        var newHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (string.Equals(newHash, fileAttachment.Hash, StringComparison.OrdinalIgnoreCase))
        {
            // Содержимое не изменилось (открыли и закрыли без правок либо сохранили то же
            // самое) — новой версии заводить не за что, подписи трогать тоже.
            _logger.LogInformation(
                "Coauthoring: вложение {AttachmentId} сохранено с прежним хешем, версия не создаётся", attachmentId);
            return;
        }

        var editorUserId = ResolveEditorUserId(body.Users) ?? fileAttachment.UploadedByUserId;

        // Первая правка: фиксируем текущее содержимое версией 1, чтобы история была
        // полной, а не начиналась со второй редакции.
        var hasVersions = await _db.AttachmentVersions.AnyAsync(v => v.FileAttachmentId == fileId, ct);
        var nextVersion = 1;
        if (!hasVersions)
        {
            _db.AttachmentVersions.Add(new AttachmentVersion
            {
                FileAttachmentId = fileId,
                Version = 1,
                StorageKey = fileAttachment.StorageKey,
                Bucket = fileAttachment.Bucket,
                ContentType = fileAttachment.ContentType,
                OriginalFileName = fileAttachment.OriginalFileName,
                SizeBytes = fileAttachment.SizeBytes,
                Hash = fileAttachment.Hash,
                CreatedByUserId = fileAttachment.UploadedByUserId,
                CreatedAt = fileAttachment.CreatedAt == default ? DateTime.UtcNow : fileAttachment.CreatedAt,
            });
            nextVersion = 2;
        }
        else
        {
            nextVersion = (await _db.AttachmentVersions
                .Where(v => v.FileAttachmentId == fileId)
                .MaxAsync(v => v.Version, ct)) + 1;
        }

        var stored = await _versionStorage.SaveVersionAsync(
            bytes, fileAttachment.OriginalFileName, fileAttachment.ContentType, ct);

        _db.AttachmentVersions.Add(new AttachmentVersion
        {
            FileAttachmentId = fileId,
            Version = nextVersion,
            StorageKey = stored.StorageKey,
            Bucket = stored.Bucket,
            ContentType = fileAttachment.ContentType,
            OriginalFileName = fileAttachment.OriginalFileName,
            SizeBytes = stored.Size,
            Hash = stored.Hash,
            CreatedByUserId = editorUserId,
            CreatedAt = DateTime.UtcNow,
        });

        // FileAttachment всегда указывает на последнюю версию: прежний объект в MinIO
        // остаётся (не перезатёрт), но актуальным становится новый.
        fileAttachment.StorageKey = stored.StorageKey;
        fileAttachment.Bucket = stored.Bucket;
        fileAttachment.Hash = stored.Hash;
        fileAttachment.SizeBytes = stored.Size;

        // Хеш вложения карточки удостоверяет подпись (SIG-01): при смене содержимого он
        // обязан обновиться, иначе скачивание карточки начнёт падать на сверке хеша.
        var cardAttachments = await _db.DocumentAttachments
            .Where(a => a.FileRef == fileId.ToString())
            .ToListAsync(ct);

        foreach (var card in cardAttachments)
        {
            card.Hash = stored.Hash;
            card.Size = stored.Size;
        }

        await _db.SaveChangesAsync(ct);

        // Подписи под прежней версией недействительны для новой — аннулируем, как при
        // обычной замене файла.
        foreach (var card in cardAttachments)
            await _signatures.RevokeForAttachmentAsync(card.Id,
                "Файл изменён при онлайн-соредактировании — подпись удостоверяла прежнюю версию");

        await _audit.LogAsync("CoauthoringSession", attachmentId, "VersionSaved", editorUserId,
            new { fileAttachmentId = fileId, version = nextVersion, hash = stored.Hash });

        _logger.LogInformation(
            "Coauthoring: вложение {AttachmentId} сохранено версией {Version} (файл {FileId})",
            attachmentId, nextVersion, fileId);
    }

    private static string DocumentTypeFor(string ext) => ext switch
    {
        ".docx" => "word",
        ".xlsx" => "cell",
        ".pptx" => "slide",
        _ => throw new InvalidOperationException($"Формат {ext} не поддерживается"),
    };

    private static int? ResolveEditorUserId(List<string>? users)
    {
        if (users is null) return null;
        foreach (var u in Enumerable.Reverse(users))
            if (int.TryParse(u, out var id))
                return id;
        return null;
    }
}
