using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Hr.Controllers;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Приказ об отмене гасит отменяемый.
///
/// Ссылка «отменяет приказ №…» сохранялась, а сам отменённый приказ оставался
/// подписанным: в реестре и в кадровой истории сотрудника отменённая
/// командировка выглядела действующей. Статус «Отменён» был объявлен и не
/// выставлялся нигде.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HrOrderCancellationTests
{
    private readonly PostgresFixture _postgres;

    public HrOrderCancellationTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Подписание_отменяющего_приказа_гасит_отменяемый()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Контроллер(db, стенд.Actor).Sign(стенд.CancellingId, default);

        var отменённый = await db.Set<HrOrder>().AsNoTracking()
            .FirstAsync(o => o.Id == стенд.OriginalId);

        Assert.Equal(HrOrderStatus.Cancelled, отменённый.Status);
    }

    [Fact]
    public async Task Отменяющий_приказ_остаётся_подписанным()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Контроллер(db, стенд.Actor).Sign(стенд.CancellingId, default);

        var отменяющий = await db.Set<HrOrder>().AsNoTracking()
            .FirstAsync(o => o.Id == стенд.CancellingId);

        Assert.Equal(HrOrderStatus.Signed, отменяющий.Status);
        Assert.False(string.IsNullOrWhiteSpace(отменяющий.RegNumber));
    }

    [Fact]
    public async Task Пока_отменяющий_черновик_исходный_действует()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Приказ подготовлен, но не подписан — передумать ещё можно, и гасить
        // исходный рано.
        var исходный = await db.Set<HrOrder>().AsNoTracking()
            .FirstAsync(o => o.Id == стенд.OriginalId);

        Assert.Equal(HrOrderStatus.Signed, исходный.Status);
    }

    [Fact]
    public async Task Обычный_приказ_ничего_не_гасит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await db.Set<HrOrder>().Where(o => o.Id == стенд.CancellingId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CancelsOrderId, (int?) null));

        // ExecuteUpdate идёт мимо трекера: без сброса контроллер прочитал бы
        // приказ из кеша контекста — со старой ссылкой на отменяемый.
        db.ChangeTracker.Clear();

        await Контроллер(db, стенд.Actor).Sign(стенд.CancellingId, default);

        var исходный = await db.Set<HrOrder>().AsNoTracking()
            .FirstAsync(o => o.Id == стенд.OriginalId);

        Assert.Equal(HrOrderStatus.Signed, исходный.Status);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int OriginalId, int CancellingId, int Actor);

    private static HrOrderController Контроллер(DelosferaDbContext db, int userId)
    {
        var audit = new AuditService(db);

        return new HrOrderController(
            db, new FakeCurrentUser(userId), new PassthroughHtml(),
            new AcknowledgementService(
                db, new FakeSignatures(), new SilentNotifications(), audit,
                NullLogger<AcknowledgementService>.Instance),
            new NoopFileStorage());
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var кадровик = await ПользовательАsync(db, "Кадровик");
        var сотрудник = await ПользовательАsync(db, "Командированный");

        var исходный = new HrOrder
        {
            Kind = HrOrderKind.BusinessTrip,
            Status = HrOrderStatus.Signed,
            Title = "О направлении в командировку",
            Year = 2026,
            RegNumber = "10-лс",
            CreatedByUserId = кадровик,
            Employees = [new HrOrderEmployee {UserId = сотрудник}],
        };
        db.Set<HrOrder>().Add(исходный);
        await db.SaveChangesAsync();

        var отменяющий = new HrOrder
        {
            Kind = HrOrderKind.BusinessTrip,
            Status = HrOrderStatus.Draft,
            Title = "Об отмене командировки",
            Year = 2026,
            CreatedByUserId = кадровик,
            CancelsOrderId = исходный.Id,
            Employees = [new HrOrderEmployee {UserId = сотрудник}],
        };
        db.Set<HrOrder>().Add(отменяющий);
        await db.SaveChangesAsync();

        return new Стенд(исходный.Id, отменяющий.Id, кадровик);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"hrc-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
