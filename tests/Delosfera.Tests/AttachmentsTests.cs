using delosfera_server.Data;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Correspondence.Services;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.PowerOfAttorney.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Файлы письма и доверенности.
///
/// Обе модели существовали, карточка письма отдавала счётчик файлов — а точки
/// загрузки не было ни одной. Регистрация письма оставалась записью в книге без
/// самого письма, реестр доверенностей отвечал на вопрос «вправе ли он
/// подписать» одними реквизитами.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AttachmentsTests
{
    private readonly PostgresFixture _postgres;

    public AttachmentsTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task К_письму_прикладывается_скан()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var письма = Письма(db, стенд.Actor);

        var файл = await письма.AddFileAsync(стенд.LetterId, Файл("скан.pdf"), стенд.Actor);

        Assert.Equal("скан.pdf", файл.FileName);
        Assert.Single(await письма.FilesAsync(стенд.LetterId));
    }

    [Fact]
    public async Task У_скана_письма_запоминается_хеш()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var файл = await Письма(db, стенд.Actor)
            .AddFileAsync(стенд.LetterId, Файл("скан.pdf"), стенд.Actor);

        // Скан признаётся доказательством, когда известно, кто его загрузил и что
        // он с тех пор не менялся: автора пишет журнал, неизменность — этот хеш.
        Assert.False(string.IsNullOrWhiteSpace(файл.ContentHash));
    }

    [Fact]
    public async Task Файлы_запроса_по_счетам_чужому_не_видны()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, тайна: true);

        await Письма(db, стенд.Actor).AddFileAsync(стенд.LetterId, Файл("выписка.pdf"), стенд.Actor);

        var посторонний = await ПользовательАsync(db, "Посторонний");

        // Круг допущенных к банковской тайне уже круга читающих переписку, и на
        // файлы это распространяется так же, как на само письмо.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Письма(db, посторонний).FilesAsync(стенд.LetterId));
    }

    [Fact]
    public async Task К_доверенности_прикладывается_скан()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var доверенности = Доверенности(db);

        var файл = await доверенности.AddFileAsync(стенд.PoaId, Файл("доверенность.pdf"), стенд.Actor);

        Assert.Equal("доверенность.pdf", файл.FileName);
        Assert.Single(await доверенности.FilesAsync(стенд.PoaId));
    }

    [Fact]
    public async Task Скан_несуществующей_доверенности_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Доверенности(db).AddFileAsync(999_999, Файл("чужое.pdf"), стенд.Actor));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int LetterId, int PoaId, int Actor);

    private static ILetterService Письма(DelosferaDbContext db, int userId) =>
        new LetterService(db, new FakeCurrentUser(userId), new ХранилищеВПамяти(db));

    private static IPoaService Доверенности(DelosferaDbContext db) =>
        new PoaService(db, new ХранилищеВПамяти(db));

    private static IFormFile Файл(string name)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 проба");
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
    }

    /// <summary>Хранилище без MinIO: проверяется связывание файла с карточкой, не сеть.</summary>
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
            int fileId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(int fileId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<FileAttachment> SaveGeneratedAsync(
            byte[] content, string fileName, string contentType, int userId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string> ComputeHashAsync(IFormFile file, CancellationToken ct = default) =>
            Task.FromResult(Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(file.FileName))).ToLowerInvariant());
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"att-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db, bool тайна = false)
    {
        var автор = await ПользовательАsync(db, "Делопроизводитель");

        var корреспондент = new Correspondent {Title = "Национальный банк"};
        db.Correspondents.Add(корреспондент);
        await db.SaveChangesAsync();

        var письмо = new CorrespondenceLetter
        {
            Direction = LetterDirection.Incoming,
            Category = тайна ? LetterCategory.BankSecrecyInquiry : LetterCategory.Ordinary,
            CorrespondentId = корреспондент.Id,
            Subject = "Запрос сведений",
            RegNumber = $"вх-{Guid.NewGuid():N}"[..12],
            RegisteredOn = new DateOnly(2026, 8, 30),
            CreatedByUserId = автор,
        };
        db.CorrespondenceLetters.Add(письмо);

        var доверенность = new delosfera_server.Modules.PowerOfAttorney.Models.PowerOfAttorney
        {
            Year = 2026,
            RegNumber = "Д-2026-001",
            HolderName = "Иванов И.И.",
            GrantorUserId = автор,
            IssuedOn = new DateOnly(2026, 1, 15),
            ValidTo = new DateOnly(2026, 12, 31),
            Powers = "Представление интересов банка",
        };
        db.PowersOfAttorney.Add(доверенность);
        await db.SaveChangesAsync();

        return new Стенд(письмо.Id, доверенность.Id, автор);
    }
}
