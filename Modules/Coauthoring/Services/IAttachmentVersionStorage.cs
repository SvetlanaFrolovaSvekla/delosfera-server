using System.Security.Cryptography;
using Minio;
using Minio.DataModel.Args;

namespace delosfera_server.Modules.Coauthoring.Services;

/// <summary>Сохранённый в MinIO объект версии: координаты и свёртка содержимого.</summary>
public sealed record StoredObject(string Bucket, string StorageKey, string Hash, long Size);

/// <summary>
/// Кладёт содержимое отдельной версии вложения в MinIO НОВЫМ объектом (COOP-1),
/// не трогая таблицу FileAttachment и не перезатирая прежние объекты. В отличие от
/// <see cref="delosfera_server.Modules.Files.Services.IFileStorageService.SaveGeneratedAsync"/>,
/// не заводит новую запись FileAttachment — версия привязывается к уже существующему
/// вложению вызывающим сервисом.
/// </summary>
public interface IAttachmentVersionStorage
{
    Task<StoredObject> SaveVersionAsync(
        byte[] content, string fileName, string contentType, CancellationToken ct = default);
}

public sealed class MinioAttachmentVersionStorage : IAttachmentVersionStorage
{
    private readonly IMinioClient _minio;
    private readonly string _bucket;

    public MinioAttachmentVersionStorage(IMinioClient minio, IConfiguration config)
    {
        _minio = minio;
        _bucket = config["Minio:Bucket"]!;
    }

    public async Task<StoredObject> SaveVersionAsync(
        byte[] content, string fileName, string contentType, CancellationToken ct = default)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));

        // GUID-префикс даёт уникальность и защищает от подстановки пути в имени файла —
        // тот же приём, что в MinioFileStorageService.SaveAsync.
        var objectName = $"{Guid.NewGuid()}/v/{delosfera_server.Modules.Files.Services.MinioFileStorageService.SafeKeySegment(fileName)}";

        await using (var stream = new MemoryStream(content))
        {
            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(content.LongLength)
                .WithContentType(contentType), ct);
        }

        return new StoredObject(_bucket, objectName, hash, content.LongLength);
    }
}
