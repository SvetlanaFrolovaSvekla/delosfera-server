using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using Xunit;

namespace Delosfera.Tests;

/// <summary>Хеш-цепь аудита (AUD-1): сцепление, обнаружение подмены, бэкфилл легаси.</summary>
[Collection(PostgresCollection.Name)]
public class AuditChainTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Log_ChainsEntries_AndVerifies()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var audit = new AuditService(db);

        await audit.LogAsync("Doc", 1, "Created", 10);
        await audit.LogAsync("Doc", 1, "Approved", 11, new { step = "УЧР" });
        await audit.LogAsync("Doc", 1, "Signed", 12);

        var entries = await db.AuditEntries.AsNoTracking().OrderBy(a => a.Id).ToListAsync();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Hash)));
        Assert.Null(entries[0].PrevHash);                    // первая запись — начало цепи
        Assert.Equal(entries[0].Hash, entries[1].PrevHash);  // сцеплено
        Assert.Equal(entries[1].Hash, entries[2].PrevHash);

        var status = await audit.VerifyChainAsync();
        Assert.True(status.Valid);
        Assert.Equal(3, status.CheckedCount);
    }

    [Fact]
    public async Task Verify_DetectsTampering()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var audit = new AuditService(db);

        await audit.LogAsync("Doc", 1, "Created", 10);
        await audit.LogAsync("Doc", 1, "Approved", 11);
        await audit.LogAsync("Doc", 1, "Signed", 12);

        // Ретроактивная правда: меняем действие второй записи в обход служб (как сделал бы злоумышленник в БД).
        var victim = await db.AuditEntries.OrderBy(a => a.Id).Skip(1).FirstAsync();
        victim.Action = "Rejected";
        await db.SaveChangesAsync();

        var status = await audit.VerifyChainAsync();
        Assert.False(status.Valid);
        Assert.Equal(victim.Id, status.BrokenAtId);
    }

    [Fact]
    public async Task Backfill_ChainsLegacyEntries()
    {
        await using var db = await postgres.NewIsolatedDbAsync();

        // Легаси без хеша — как записи, созданные до внедрения AUD-1.
        db.AuditEntries.AddRange(
            new AuditEntry { EntityType = "Doc", EntityId = 1, Action = "Created", UserId = 1, At = DateTime.UtcNow },
            new AuditEntry { EntityType = "Doc", EntityId = 1, Action = "Approved", UserId = 2, At = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var filled = await audit.BackfillChainAsync();
        Assert.Equal(2, filled);

        var status = await audit.VerifyChainAsync();
        Assert.True(status.Valid);
        Assert.Equal(2, status.CheckedCount);

        // Новая запись продолжает уже достроенную цепь.
        await audit.LogAsync("Doc", 1, "Signed", 3);
        Assert.True((await audit.VerifyChainAsync()).Valid);
    }
}
