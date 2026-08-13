using System.Security.Cryptography;
using System.Text;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Связь вложений карточки с подписями (SIG-01).
///
/// Проверяется главное обещание юридической значимости: подпись удостоверяет
/// конкретную версию файла, и замена файла её аннулирует.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DocumentAttachmentTests
{
    private readonly PostgresFixture _postgres;

    public DocumentAttachmentTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task ReplaceFile_RevokesSignaturesOfPreviousVersion()
    {
        await using var db = _postgres.NewDb();
        var (service, signatures) = NewService(db);

        var documentId = await SeedDocumentAsync(db);
        var added = await service.AddAsync(documentId, FakeFile("исходный текст", "приказ.txt"), userId: 1);

        await signatures.SignAsync(added.Id, SignatureLevel.Qualified, userId: 1);

        var afterSigning = (await service.ListAsync(documentId)).Single();
        Assert.Equal(1, afterSigning.SignatureCount);

        await service.ReplaceAsync(added.Id, FakeFile("исправленный текст", "приказ.txt"), userId: 1);

        var afterReplace = (await service.ListAsync(documentId)).Single();
        Assert.Equal(0, afterReplace.SignatureCount);
        Assert.True(afterReplace.HasRevokedSignatures);

        // Проверяем подписи именно этого вложения: база в прогоне общая, и «единственная
        // подпись во всей базе» — условие, которое ломается от соседнего теста.
        var signature = await db.Signatures.AsNoTracking()
            .SingleAsync(x => x.DocumentAttachmentId == added.Id);

        Assert.True(signature.Revoked);
        Assert.Contains("Файл заменён", signature.RevokedReason);
    }

    [Fact]
    public async Task ReplaceWithIdenticalFile_KeepsSignatures()
    {
        await using var db = _postgres.NewDb();
        var (service, signatures) = NewService(db);

        var documentId = await SeedDocumentAsync(db);
        var added = await service.AddAsync(documentId, FakeFile("текст без изменений", "акт.txt"), userId: 1);

        await signatures.SignAsync(added.Id, SignatureLevel.Simple, userId: 1);

        // Повторная загрузка того же содержимого — не изменение документа,
        // и наказывать за неё аннулированием подписи неправильно.
        await service.ReplaceAsync(added.Id, FakeFile("текст без изменений", "акт.txt"), userId: 1);

        var afterReplace = (await service.ListAsync(documentId)).Single();
        Assert.Equal(1, afterReplace.SignatureCount);
        Assert.False(afterReplace.HasRevokedSignatures);
    }

    [Fact]
    public async Task Hash_IsSha256OfContent_AndIsWhatGetsSigned()
    {
        await using var db = _postgres.NewDb();
        var (service, _) = NewService(db);

        const string content = "Протокол заседания КПА № 01";
        var documentId = await SeedDocumentAsync(db);
        var added = await service.AddAsync(documentId, FakeFile(content, "протокол.txt"), userId: 1);

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        Assert.Equal(expected, added.Hash);
    }

    [Fact]
    public async Task Delete_RevokesSignaturesButKeepsThemInHistory()
    {
        await using var db = _postgres.NewDb();
        var (service, signatures) = NewService(db);

        var documentId = await SeedDocumentAsync(db);
        var added = await service.AddAsync(documentId, FakeFile("к удалению", "черновик.txt"), userId: 1);
        await signatures.SignAsync(added.Id, SignatureLevel.Simple, userId: 1);

        await service.DeleteAsync(added.Id, userId: 1);

        Assert.Empty(await service.ListAsync(documentId));

        // Факт подписания остаётся в истории: стирать его вместе с файлом нельзя.
        var signature = await db.Signatures.AsNoTracking()
            .SingleAsync(x => x.DocumentAttachmentId == added.Id);

        Assert.True(signature.Revoked);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private (IDocumentAttachmentService Service, ISignatureService Signatures) NewService(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var signatures = new SignatureService(db, audit);

        var service = new DocumentAttachmentService(
            db, new InMemoryFileStorage(db), signatures, audit,
            NullLogger<DocumentAttachmentService>.Instance);

        return (service, signatures);
    }

    private static async Task<int> SeedDocumentAsync(DelosferaDbContext db)
    {
        var author = new User
        {
            FullName = "Тест Тестов",
            Email = $"attach-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(author);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Документ для проверки вложений",
            StatusCode = "Draft",
            AuthorId = author.Id,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return document.Id;
    }

    private static IFormFile FakeFile(string content, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain",
        };
    }

    /// <summary>
    /// Хранилище файлов в памяти: MinIO для этих проверок не нужен — они про связь
    /// вложения с подписью, а не про сеть и бакеты.
    /// </summary>
    private sealed class InMemoryFileStorage : IFileStorageService
    {
        private static readonly Dictionary<int, byte[]> Content = new();

        private readonly DelosferaDbContext _db;

        public InMemoryFileStorage(DelosferaDbContext db) => _db = db;

        public async Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);

            var entity = new FileAttachment
            {
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = Guid.NewGuid().ToString("N"),
                UploadedByUserId = userId,
            };

            _db.FileAttachments.Add(entity);
            await _db.SaveChangesAsync(ct);

            Content[entity.Id] = buffer.ToArray();
            return entity;
        }

        public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(
            int fileId, CancellationToken ct = default)
        {
            var entity = _db.FileAttachments.Single(f => f.Id == fileId);
            return Task.FromResult<(Stream, string, string)>(
                (new MemoryStream(Content[fileId]), entity.ContentType, entity.OriginalFileName));
        }

        public Task DeleteAsync(int fileId, CancellationToken ct = default)
        {
            Content.Remove(fileId);
            return Task.CompletedTask;
        }
    }
}
