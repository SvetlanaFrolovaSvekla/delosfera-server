using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using delosfera_server.Data;
using delosfera_server.Modules.Coauthoring.DTO;
using delosfera_server.Modules.Coauthoring.Options;
using delosfera_server.Modules.Coauthoring.Services;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Delosfera.Tests;

/// <summary>
/// Онлайн-соредактирование вложений через OnlyOffice (COOP-1).
///
/// Проверяется главное: конфиг редактора генерируется с корректной подписью и правами,
/// а результат правок из callback'а ложится НОВОЙ версией в хранилище, не перезатирая
/// прежнюю, — с обновлением актуального файла и записью в аудит.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CoauthoringTests
{
    private const string Secret = "coauthoring-test-secret-0123456789abcdef";
    private readonly PostgresFixture _postgres;

    public CoauthoringTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Config_ForAuthor_IsEditMode_AndTokenValidatesWithSecret()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, _) = await SeedAttachmentAsync(db, "договор.docx");

        var service = NewService(db, new FakeCurrentUser(authorId), access: true);

        var config = await service.BuildConfigAsync(attachmentId, "https://api.local");

        Assert.Equal("word", config.DocumentType);
        Assert.Equal("edit", config.EditorConfig.Mode);
        Assert.True(config.Document.Permissions.Edit);
        Assert.Equal("docx", config.Document.FileType);
        Assert.Contains($"/api/coauthoring/download/{attachmentId}", config.Document.Url);
        Assert.Contains($"attachmentId={attachmentId}", config.EditorConfig.CallbackUrl);

        // Токен конфига подписан общим секретом и проверяется им же — иначе DS откажется открывать.
        Assert.True(OnlyOfficeJwt.TryValidate(config.Token, Secret, out var payload));
        Assert.True(payload.TryGetProperty("document", out _));

        // Чужим секретом та же подпись не проходит.
        Assert.False(OnlyOfficeJwt.TryValidate(config.Token, "another-secret-value-000000000000", out _));
    }

    [Fact]
    public async Task Config_ForNonAuthorWithAccess_IsReviewMode_NotEdit()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, _) = await SeedAttachmentAsync(db, "смета.xlsx");

        // Не автор, но файл ему доступен (access=true).
        var service = NewService(db, new FakeCurrentUser(authorId + 500), access: true);

        var config = await service.BuildConfigAsync(attachmentId, "https://api.local");

        Assert.Equal("cell", config.DocumentType);
        Assert.Equal("review", config.EditorConfig.Mode);
        Assert.False(config.Document.Permissions.Edit);
        Assert.True(config.Document.Permissions.Comment);
        Assert.True(config.Document.Permissions.Review);
    }

    [Fact]
    public async Task Config_WithoutFileAccess_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, _, _) = await SeedAttachmentAsync(db, "акт.docx");

        var service = NewService(db, new FakeCurrentUser(999), access: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.BuildConfigAsync(attachmentId, "https://api.local"));
    }

    [Fact]
    public async Task Config_ForNonEditableType_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, _) = await SeedAttachmentAsync(db, "скан.pdf");

        var service = NewService(db, new FakeCurrentUser(authorId), access: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuildConfigAsync(attachmentId, "https://api.local"));
    }

    [Fact]
    public async Task Callback_Status2_SavesEditedFileAsNewVersion_WithoutOverwritingPrevious()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, fileId) = await SeedAttachmentAsync(db, "положение.docx");

        var originalKey = (await db.FileAttachments.AsNoTracking().SingleAsync(f => f.Id == fileId)).StorageKey;
        var originalHash = (await db.FileAttachments.AsNoTracking().SingleAsync(f => f.Id == fileId)).Hash;

        var storage = new FakeVersionStorage();
        var edited = Encoding.UTF8.GetBytes("отредактированное содержимое docx");
        var service = NewService(db, new FakeCurrentUser(authorId), access: true,
            storage: storage, fetcher: new FakeFetcher(edited));

        await service.HandleCallbackAsync(attachmentId, SignedCallback(status: 2,
            url: "https://ds.local/cache/edited.docx", users: [authorId.ToString()]));

        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(edited));

        // Две версии: baseline (v1 = исходный объект) + v2 (результат правок).
        var versions = await db.AttachmentVersions.AsNoTracking()
            .Where(v => v.FileAttachmentId == fileId).OrderBy(v => v.Version).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(originalKey, versions[0].StorageKey);            // прежний объект сохранён в истории
        Assert.Equal(originalHash, versions[0].Hash);
        Assert.Equal(expectedHash, versions[1].Hash);
        Assert.NotEqual(originalKey, versions[1].StorageKey);         // новая версия — отдельный объект

        // FileAttachment указывает на новую версию, но прежний объект НЕ перезатёрт.
        var file = await db.FileAttachments.AsNoTracking().SingleAsync(f => f.Id == fileId);
        Assert.Equal(expectedHash, file.Hash);
        Assert.Equal(versions[1].StorageKey, file.StorageKey);
        Assert.True(storage.Saved.ContainsKey(originalKey) == false); // baseline не перекладывали
        Assert.Contains(storage.Saved.Keys, k => k == versions[1].StorageKey);

        // Хеш вложения карточки обновлён (SIG-01): скачивание карточки продолжит сходиться.
        var card = await db.DocumentAttachments.AsNoTracking().SingleAsync(a => a.Id == attachmentId);
        Assert.Equal(expectedHash, card.Hash);

        // Событие зафиксировано в аудите.
        Assert.True(await db.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.EntityType == "CoauthoringSession" && e.EntityId == attachmentId && e.Action == "VersionSaved"));
    }

    [Fact]
    public async Task Callback_WithInvalidToken_IsRejected_AndSavesNothing()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, fileId) = await SeedAttachmentAsync(db, "инструкция.docx");

        var service = NewService(db, new FakeCurrentUser(authorId), access: true,
            fetcher: new FakeFetcher(Encoding.UTF8.GetBytes("правки")));

        var forged = new OnlyOfficeCallbackRequest
        {
            Status = 2,
            Url = "https://ds.local/cache/edited.docx",
            Token = OnlyOfficeJwt.Sign(new { status = 2 }, "wrong-secret-000000000000000000"),
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.HandleCallbackAsync(attachmentId, forged));

        Assert.Empty(await db.AttachmentVersions.AsNoTracking().Where(v => v.FileAttachmentId == fileId).ToListAsync());
    }

    [Fact]
    public async Task Callback_NonSavingStatus_DoesNothing()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (attachmentId, authorId, fileId) = await SeedAttachmentAsync(db, "приложение.docx");

        var service = NewService(db, new FakeCurrentUser(authorId), access: true,
            fetcher: new FakeFetcher(Encoding.UTF8.GetBytes("нечто")));

        // status=1 (редактируется) — сохранять нечего.
        await service.HandleCallbackAsync(attachmentId, SignedCallback(status: 1, url: null, users: null));

        Assert.Empty(await db.AttachmentVersions.AsNoTracking().Where(v => v.FileAttachmentId == fileId).ToListAsync());
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static ICoauthoringService NewService(
        DelosferaDbContext db,
        FakeCurrentUser currentUser,
        bool access,
        IAttachmentVersionStorage? storage = null,
        IOnlyOfficeDocumentFetcher? fetcher = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OnlyOfficeOptions
        {
            ServerUrl = "https://ds.local",
            JwtSecret = Secret,
            ApiPublicUrl = "https://api.local",
            DocumentUrlTtlSeconds = 300,
        });

        return new CoauthoringService(
            db,
            new NoopFileStorage(),
            storage ?? new FakeVersionStorage(),
            fetcher ?? new FakeFetcher([]),
            new FakeFileAccess(access),
            currentUser,
            new FakeSignatures(),
            new AuditService(db),
            options,
            NullLogger<CoauthoringService>.Instance);
    }

    /// <summary>Callback с действительной подписью тела (как его шлёт настроенный DS).</summary>
    private static OnlyOfficeCallbackRequest SignedCallback(int status, string? url, List<string>? users) =>
        new()
        {
            Status = status,
            Url = url,
            Users = users,
            Token = OnlyOfficeJwt.Sign(new { status, url, users }, Secret),
        };

    private static async Task<(int AttachmentId, int AuthorId, int FileId)> SeedAttachmentAsync(
        DelosferaDbContext db, string fileName)
    {
        var author = new User
        {
            FullName = "Автор Карточки",
            Email = $"coop-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var content = Encoding.UTF8.GetBytes($"исходное содержимое {fileName}");
        var file = new FileAttachment
        {
            OriginalFileName = fileName,
            ContentType = "application/octet-stream",
            SizeBytes = content.Length,
            StorageKey = $"{Guid.NewGuid():N}/{fileName}",
            Bucket = "delosfera-vnd",
            Hash = Convert.ToHexStringLower(SHA256.HashData(content)),
            UploadedByUserId = author.Id,
        };
        db.FileAttachments.Add(file);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Документ с вложением для соредактирования",
            StatusCode = "Draft",
            AuthorId = author.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var attachment = new DocumentAttachment
        {
            DocumentId = document.Id,
            FileRef = file.Id.ToString(),
            FileName = fileName,
            Hash = file.Hash!,
            Size = file.SizeBytes,
            UploadedById = author.Id,
        };
        db.DocumentAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return (attachment.Id, author.Id, file.Id);
    }

    private sealed class FakeFileAccess(bool allowed) : IFileAccessAuthorizer
    {
        public Task<bool> CanCurrentUserAccessAsync(int fileId, CancellationToken ct = default) =>
            Task.FromResult(allowed);
    }

    /// <summary>Хранилище версий в памяти: MinIO для этих проверок не нужен.</summary>
    private sealed class FakeVersionStorage : IAttachmentVersionStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = new();

        public Task<StoredObject> SaveVersionAsync(
            byte[] content, string fileName, string contentType, CancellationToken ct = default)
        {
            var key = $"{Guid.NewGuid():N}/v/{fileName}";
            Saved[key] = content;
            var hash = Convert.ToHexStringLower(SHA256.HashData(content));
            return Task.FromResult(new StoredObject("test-bucket", key, hash, content.LongLength));
        }
    }

    /// <summary>Скачивание отредактированного файла — заглушка вместо HTTP к Document Server.</summary>
    private sealed class FakeFetcher(byte[] bytes) : IOnlyOfficeDocumentFetcher
    {
        public Task<byte[]> FetchAsync(string url, CancellationToken ct = default) => Task.FromResult(bytes);
    }
}
