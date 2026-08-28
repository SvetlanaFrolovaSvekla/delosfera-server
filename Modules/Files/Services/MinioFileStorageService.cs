using System.Security.Cryptography;
using delosfera_server.Data;
using delosfera_server.Modules.Files.Models;
using Minio;
using Minio.DataModel.Args;

namespace delosfera_server.Modules.Files.Services;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minio;
    private readonly DelosferaDbContext _db;
    private readonly string _bucket;

    public MinioFileStorageService(IMinioClient minio, DelosferaDbContext db, IConfiguration config)
    {
        _minio = minio;
        _db = db;
        _bucket = config["Minio:Bucket"]!;
    }

    public async Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default)
    {
        var ext = ValidateFile(file);
        await ValidateContentSignatureAsync(file, ext, ct);
        var hash = await ComputeHashAsync(file, ct);

        var objectName = $"{Guid.NewGuid()}/{file.FileName}";

        await using var stream = file.OpenReadStream();
        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(file.ContentType), ct);

        var entity = new FileAttachment
        {
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StorageKey = objectName,
            Bucket = _bucket,
            Hash = hash,
            UploadedByUserId = userId
        };

        _db.FileAttachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<FileAttachment> SaveGeneratedAsync(
        byte[] content, string fileName, string contentType, int userId, CancellationToken ct = default)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var objectName = $"{Guid.NewGuid()}/{fileName}";

        await using (var stream = new MemoryStream(content))
        {
            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(content.LongLength)
                .WithContentType(contentType), ct);
        }

        var entity = new FileAttachment
        {
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            StorageKey = objectName,
            Bucket = _bucket,
            Hash = hash,
            UploadedByUserId = userId
        };

        _db.FileAttachments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<(Stream, string, string)> DownloadAsync(int fileId, CancellationToken ct = default)
    {
        var meta = await _db.FileAttachments.FindAsync([fileId], ct)
            ?? throw new KeyNotFoundException($"Файл с id={fileId} не найден");

        var ms = new MemoryStream();
        await _minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(meta.Bucket)
            .WithObject(meta.StorageKey)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;

        return (ms, meta.ContentType, meta.OriginalFileName);
    }

    public async Task DeleteAsync(int fileId, CancellationToken ct = default)
    {
        var meta = await _db.FileAttachments.FindAsync([fileId], ct);
        if (meta is null) return;

        await _minio.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(meta.Bucket)
            .WithObject(meta.StorageKey), ct);

        _db.FileAttachments.Remove(meta);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> ComputeHashAsync(IFormFile file, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        var bytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(bytes);
    }

    // Изображения нужны наравне с документами: к записке и заявке чаще всего
    // прикладывают снимок экрана — счёта, ошибки, переписки, — и запрет на них
    // означал бы, что вставка из буфера не работает вовсе.
    private static readonly string[] AllowedExtensions =
        [".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx", ".png", ".jpg", ".jpeg"];

    // Сигнатуры содержимого (magic bytes) — расширение можно подделать, поэтому проверяем и начало файла.
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();                        // .pdf
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];                   // OOXML .docx/.xlsx/.pptx (zip)
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]; // legacy .doc/.xls/.ppt
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // .png
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];                        // .jpg/.jpeg

    /// <returns>Нормализованное расширение (в нижнем регистре).</returns>
    private static string ValidateFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"Недопустимый формат файла: {ext}");

        if (file.Length > 50 * 1024 * 1024) // 50 МБ
            throw new InvalidOperationException("Файл превышает допустимый размер (50 МБ)");

        return ext;
    }

    /// <summary>
    /// Проверяет, что фактическое содержимое файла соответствует расширению
    /// (защита от загрузки исполняемого/произвольного файла под видом документа).
    /// </summary>
    private static async Task ValidateContentSignatureAsync(IFormFile file, string ext, CancellationToken ct)
    {
        var expected = ext switch
        {
            ".pdf" => PdfSignature,
            ".docx" or ".xlsx" or ".pptx" => ZipSignature,
            ".doc" or ".xls" or ".ppt" => OleSignature,
            ".png" => PngSignature,
            ".jpg" or ".jpeg" => JpegSignature,
            _ => throw new InvalidOperationException($"Недопустимый формат файла: {ext}")
        };

        var header = new byte[expected.Length];
        await using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAsync(header, ct);
            if (read < expected.Length || !header.AsSpan().SequenceEqual(expected))
                throw new InvalidOperationException("Содержимое файла не соответствует его расширению");
        }
    }
}