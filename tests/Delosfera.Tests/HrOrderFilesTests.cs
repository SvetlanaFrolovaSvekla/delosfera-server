using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Hr.Controllers;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Скан подписанного приказа.
///
/// Приказ по личному составу подписывают на бумаге и подшивают в личное дело, а
/// приложить скан к карточке было нечем: ни модели файла, ни точки загрузки.
/// Карточка оставалась записью о приказе, а не самим приказом.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HrOrderFilesTests
{
    private readonly PostgresFixture _postgres;

    public HrOrderFilesTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task К_приказу_прикладывается_скан()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var ответ = await Контроллер(db, стенд.Actor)
            .AddFile(стенд.OrderId, Файл("приказ-1-лс.pdf"), default);

        Assert.IsType<OkObjectResult>(ответ);
        Assert.Equal(1, await db.HrOrderFiles.CountAsync(f => f.OrderId == стенд.OrderId));
    }

    [Fact]
    public async Task Сканы_видны_в_списке_приказа()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var контроллер = Контроллер(db, стенд.Actor);

        await контроллер.AddFile(стенд.OrderId, Файл("приказ.pdf"), default);
        await контроллер.AddFile(стенд.OrderId, Файл("приложение.pdf"), default);

        var ok = Assert.IsType<OkObjectResult>(await контроллер.Files(стенд.OrderId, default));
        var список = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value);

        Assert.Equal(2, список.Cast<object>().Count());
    }

    [Fact]
    public async Task Пустой_файл_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var ответ = await Контроллер(db, стенд.Actor).AddFile(стенд.OrderId, Файл("пусто.pdf", пустой: true), default);

        Assert.IsType<BadRequestObjectResult>(ответ);
    }

    [Fact]
    public async Task Скан_несуществующего_приказа_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var ответ = await Контроллер(db, стенд.Actor).AddFile(999_999, Файл("чужое.pdf"), default);

        Assert.IsType<NotFoundResult>(ответ);
    }

    [Fact]
    public async Task Скан_убирается_а_файл_в_хранилище_остаётся()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var контроллер = Контроллер(db, стенд.Actor);

        await контроллер.AddFile(стенд.OrderId, Файл("не-тот.pdf"), default);
        var привязка = await db.HrOrderFiles.AsNoTracking().FirstAsync(f => f.OrderId == стенд.OrderId);

        await контроллер.RemoveFile(стенд.OrderId, привязка.Id, default);

        Assert.Empty(await db.HrOrderFiles.Where(f => f.OrderId == стенд.OrderId).ToListAsync());

        // Файл мог быть приложен и в другом месте: снятие привязки — исправление
        // ошибки вложения, а не уничтожение документа.
        Assert.True(await db.Set<FileAttachment>().AnyAsync(f => f.Id == привязка.FileId));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int OrderId, int Actor);

    private static HrOrderController Контроллер(DelosferaDbContext db, int userId)
    {
        var audit = new AuditService(db);

        return new HrOrderController(
            db, new FakeCurrentUser(userId), new PassthroughHtml(),
            new AcknowledgementService(
                db, new FakeSignatures(), new SilentNotifications(), audit,
                NullLogger<AcknowledgementService>.Instance),
            new ХранилищеВПамяти(db));
    }

    private static IFormFile Файл(string name, bool пустой = false)
    {
        var bytes = пустой
            ? []
            : System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 скан приказа");

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }

    /// <summary>Хранилище без MinIO: проверяется связывание файла с приказом, не сеть.</summary>
    private sealed class ХранилищеВПамяти : IFileStorageService
    {
        private readonly DelosferaDbContext _db;

        public ХранилищеВПамяти(DelosferaDbContext db) => _db = db;

        public async Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default)
        {
            var attachment = new FileAttachment
            {
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = $"test/{Guid.NewGuid():N}",
                UploadedByUserId = userId,
            };

            _db.Set<FileAttachment>().Add(attachment);
            await _db.SaveChangesAsync(ct);

            return attachment;
        }

        public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(
            int fileId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteAsync(int fileId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var кадровик = new User
        {
            FullName = "Кадровик",
            Email = $"hrf-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        var сотрудник = new User
        {
            FullName = "Сотрудник",
            Email = $"hrf-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.AddRange(кадровик, сотрудник);
        await db.SaveChangesAsync();

        var order = new HrOrder
        {
            Kind = HrOrderKind.BusinessTrip,
            Status = HrOrderStatus.Signed,
            Title = "О направлении в командировку",
            Year = 2026,
            RegNumber = "1-лс",
            CreatedByUserId = кадровик.Id,
            Employees = [new HrOrderEmployee {UserId = сотрудник.Id}],
        };
        db.HrOrders.Add(order);
        await db.SaveChangesAsync();

        return new Стенд(order.Id, кадровик.Id);
    }
}
