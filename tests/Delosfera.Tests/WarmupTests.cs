using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Прогрев доступа к данным.
///
/// Он выполняется на старте, поэтому важнее его пользы то, чего он делать не должен:
/// падать на пустой базе и что-либо менять. Приложение обязано подниматься и без
/// удавшегося прогрева — первый запрос тогда просто окажется медленным.
/// </summary>
[Collection(PostgresCollection.Name)]
public class WarmupTests
{
    private readonly PostgresFixture _postgres;

    public WarmupTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task На_пустой_базе_не_падает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await WarmupWorker.WarmAsync(db);
    }

    [Fact]
    public async Task Ничего_не_меняет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var author = new delosfera_server.Modules.Users.Models.User
        {
            FullName = "Автор записки",
            Email = $"warmup-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка до прогрева",
            StatusCode = "Draft",
            AuthorId = author.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var былоДокументов = await db.Documents.CountAsync();
        var былоПользователей = await db.Users.CountAsync();

        await WarmupWorker.WarmAsync(db);

        Assert.Equal(былоДокументов, await db.Documents.CountAsync());
        Assert.Equal(былоПользователей, await db.Users.CountAsync());
        Assert.Equal("Записка до прогрева", (await db.Documents.FindAsync(document.Id))!.Title);
    }

    [Fact]
    public async Task Прерывается_по_остановке_приложения()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Приложение останавливают на середине прогрева: он обязан бросить
        // отмену, а не продолжать держать соединение с базой.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WarmupWorker.WarmAsync(db, cts.Token));
    }
}
