using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Оптимистичная блокировка карточки документа (GEN-05).
///
/// Карточку правят несколько человек, и до сих пор последнее сохранение молча
/// затирало чужие правки: узнать об этом можно было только из журнала аудита
/// задним числом.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ConcurrencyTests
{
    private readonly PostgresFixture _postgres;

    public ConcurrencyTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task ParallelEdit_OfSameCard_Throws_InsteadOfSilentlyOverwriting()
    {
        await using var seed = await _postgres.NewIsolatedDbAsync();
        var documentId = await SeedDocumentAsync(seed);

        // Два сотрудника открыли одну карточку — каждый в своём подключении.
        await using var first = _postgres.NewDbFor(seed);
        await using var second = _postgres.NewDbFor(seed);

        var asSeenByFirst = await first.Documents.SingleAsync(d => d.Id == documentId);
        var asSeenBySecond = await second.Documents.SingleAsync(d => d.Id == documentId);

        asSeenByFirst.Title = "Правка делопроизводителя";
        await first.SaveChangesAsync();

        asSeenBySecond.Title = "Правка автора";

        // Второй сохраняет поверх — и получает отказ, а не тихую перезапись.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = _postgres.NewDbFor(seed);
        var stored = await check.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId);

        Assert.Equal("Правка делопроизводителя", stored.Title);
    }

    [Fact]
    public async Task SequentialEdits_AfterReload_Succeed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var documentId = await SeedDocumentAsync(db);

        var document = await db.Documents.SingleAsync(d => d.Id == documentId);
        document.Title = "Первая правка";
        await db.SaveChangesAsync();

        // Тот же контекст перечитал версию строки после сохранения — вторая правка
        // проходит. Блокировка не должна мешать обычной последовательной работе.
        document.Title = "Вторая правка";
        await db.SaveChangesAsync();

        await using var check = _postgres.NewDbFor(db);
        var stored = await check.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId);

        Assert.Equal("Вторая правка", stored.Title);
    }

    private static async Task<int> SeedDocumentAsync(DelosferaDbContext db)
    {
        var author = new User
        {
            FullName = "Автор карточки",
            Email = $"concurrency-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(author);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Исходное наименование",
            StatusCode = "Draft",
            AuthorId = author.Id,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return document.Id;
    }
}
