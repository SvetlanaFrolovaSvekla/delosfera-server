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
        ValidateFile(file);

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

    private static readonly string[] AllowedExtensions =
        [".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx"];

    private static void ValidateFile(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"Недопустимый формат файла: {ext}");

        if (file.Length > 50 * 1024 * 1024) // 50 МБ
            throw new InvalidOperationException("Файл превышает допустимый размер (50 МБ)");
    }
}