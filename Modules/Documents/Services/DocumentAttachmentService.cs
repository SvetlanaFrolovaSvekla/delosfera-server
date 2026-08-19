using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Signing.Services;

namespace delosfera_server.Modules.Documents.Services;

public class AttachmentDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>SHA-256 версии файла — именно он подписывается (SIG-01).</summary>
    public string Hash { get; set; } = string.Empty;

    public long Size { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Действующие подписи под этой версией.</summary>
    public int SignatureCount { get; set; }

    /// <summary>Подписи были аннулированы заменой файла — процесс требуется подписать заново.</summary>
    public bool HasRevokedSignatures { get; set; }
}

public interface IDocumentAttachmentService
{
    Task<List<AttachmentDto>> ListAsync(int documentId);
    Task<AttachmentDto> AddAsync(int documentId, IFormFile file, int userId, bool isPrimary = false);

    /// <summary>Заменить файл новой версией: подписи под прежней версией аннулируются (SIG-01).</summary>
    Task<AttachmentDto> ReplaceAsync(int attachmentId, IFormFile file, int userId);

    Task DeleteAsync(int attachmentId, int userId);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int attachmentId);
}

/// <summary>
/// Вложения карточки документа (GEN-05) и их связь с подписями (SIG-01).
///
/// Подписывается хеш конкретной версии файла, поэтому замена файла обязана
/// аннулировать подписи: иначе под новой редакцией стояла бы подпись, поставленная
/// под другим текстом. Раньше эта связь существовала только на бумаге — механизм
/// отзыва был написан, но вызвать его было неоткуда, потому что вложения карточки
/// не создавались нигде в системе.
///
/// Хеш считается здесь, а не берётся из хранилища: файл проходит через сервис
/// один раз, и это единственное место, где содержимое гарантированно то самое,
/// что легло в хранилище.
/// </summary>
public class DocumentAttachmentService : IDocumentAttachmentService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly ISignatureService _signatures;
    private readonly IAuditService _audit;
    private readonly ILogger<DocumentAttachmentService> _logger;

    public DocumentAttachmentService(
        DelosferaDbContext db,
        IFileStorageService files,
        ISignatureService signatures,
        IAuditService audit,
        ILogger<DocumentAttachmentService> logger)
    {
        _db = db;
        _files = files;
        _signatures = signatures;
        _audit = audit;
        _logger = logger;
    }

    public async Task<List<AttachmentDto>> ListAsync(int documentId)
    {
        var attachments = await _db.DocumentAttachments
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var ids = attachments.Select(a => a.Id).ToList();

        var signatures = await _db.Signatures
            .Where(s => s.DocumentAttachmentId != null && ids.Contains(s.DocumentAttachmentId.Value))
            .Select(s => new {s.DocumentAttachmentId, s.Revoked})
            .ToListAsync();

        return attachments.Select(a => new AttachmentDto
        {
            Id = a.Id,
            DocumentId = a.DocumentId,
            FileName = a.FileName,
            Hash = a.Hash,
            Size = a.Size,
            IsPrimary = a.IsPrimary,
            CreatedAt = a.CreatedAt,
            SignatureCount = signatures.Count(s => s.DocumentAttachmentId == a.Id && !s.Revoked),
            HasRevokedSignatures = signatures.Any(s => s.DocumentAttachmentId == a.Id && s.Revoked),
        }).ToList();
    }

    public async Task<AttachmentDto> AddAsync(
        int documentId, IFormFile file, int userId, bool isPrimary = false)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new KeyNotFoundException("Документ не найден");

        RequireFile(file);

        var hash = await ComputeHashAsync(file);
        var stored = await _files.SaveAsync(file, userId);

        // Основным может быть только одно вложение: именно оно уходит на подпись.
        if (isPrimary)
        {
            var others = await _db.DocumentAttachments
                .Where(a => a.DocumentId == documentId && a.IsPrimary)
                .ToListAsync();

            foreach (var other in others) other.IsPrimary = false;
        }

        var attachment = new DocumentAttachment
        {
            DocumentId = document.Id,
            FileRef = stored.Id.ToString(),
            FileName = file.FileName,
            Hash = hash,
            Size = file.Length,
            IsPrimary = isPrimary,
            UploadedById = userId,
        };

        _db.DocumentAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("DocumentAttachment", attachment.Id, "Added", userId,
            new {documentId, file.FileName, hash});

        return (await ListAsync(documentId)).First(a => a.Id == attachment.Id);
    }

    public async Task<AttachmentDto> ReplaceAsync(int attachmentId, IFormFile file, int userId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        RequireFile(file);

        var hash = await ComputeHashAsync(file);

        if (hash == attachment.Hash)
        {
            // Тот же самый файл: аннулировать подписи не за что, и делать это
            // означало бы наказывать за повторную загрузку без изменений.
            _logger.LogInformation(
                "Вложение {AttachmentId}: загружен файл с прежним хешем, подписи сохранены", attachmentId);

            return (await ListAsync(attachment.DocumentId)).First(a => a.Id == attachmentId);
        }

        var stored = await _files.SaveAsync(file, userId);
        var previousFileRef = attachment.FileRef;

        attachment.FileRef = stored.Id.ToString();
        attachment.FileName = file.FileName;
        attachment.Hash = hash;
        attachment.Size = file.Length;

        await _db.SaveChangesAsync();

        // SIG-01: подпись удостоверяла прежнюю версию — под новой она недействительна.
        await _signatures.RevokeForAttachmentAsync(attachmentId,
            "Файл заменён после подписания — подпись удостоверяла прежнюю версию");

        await _audit.LogAsync("DocumentAttachment", attachmentId, "Replaced", userId,
            new {previousFileRef, hash});

        return (await ListAsync(attachment.DocumentId)).First(a => a.Id == attachmentId);
    }

    public async Task DeleteAsync(int attachmentId, int userId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        // Подписи не удаляем вместе с вложением, а аннулируем: факт подписания —
        // часть истории документа, и стирать его нельзя даже вместе с файлом.
        await _signatures.RevokeForAttachmentAsync(attachmentId, "Вложение удалено из карточки");

        _db.DocumentAttachments.Remove(attachment);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("DocumentAttachment", attachmentId, "Deleted", userId,
            new {attachment.DocumentId, attachment.FileName});
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int attachmentId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        if (!int.TryParse(attachment.FileRef, out var fileId))
            throw new InvalidOperationException("Ссылка на файл в хранилище повреждена");

        var (stream, contentType, fileName) = await _files.DownloadAsync(fileId);

        // Сверяем хеш при выдаче: подпись удостоверяет его, и расхождение означает,
        // что содержимое в хранилище изменилось в обход системы. Отдать такой файл
        // как подписанный нельзя.
        //
        // Буфер не освобождается здесь: он и есть возвращаемый поток, и закрыть его
        // до того, как ASP.NET перепишет содержимое в ответ, значит отдать клиенту
        // ошибку вместо файла.
        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        await stream.DisposeAsync();

        buffer.Position = 0;
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(buffer)).ToLowerInvariant();

        if (!string.Equals(actual, attachment.Hash, StringComparison.OrdinalIgnoreCase))
        {
            await buffer.DisposeAsync();

            _logger.LogError(
                "Вложение {AttachmentId}: хеш файла в хранилище {Actual} не совпадает с записанным {Expected}",
                attachmentId, actual, attachment.Hash);

            throw new InvalidOperationException(
                "Содержимое файла в хранилище не совпадает с записанным хешем — файл изменён в обход системы");
        }

        buffer.Position = 0;
        return (buffer, contentType, fileName);
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private static void RequireFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("Файл не приложен или пуст");
    }

    private static async Task<string> ComputeHashAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var hash = await SHA256.HashDataAsync(stream);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
