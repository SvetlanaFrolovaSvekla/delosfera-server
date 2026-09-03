using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Ознакомление с кадровым приказом.
///
/// Приказ по личному составу знакомят под роспись — иначе перевод, взыскание или
/// изменение оклада остаются на бумаге. Поле листа у приказа было заведено, а
/// завести сам лист было нечем: лист умел ссылаться только на документ единой
/// карточки, на которой приказ не лежит.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HrAcknowledgementTests
{
    private readonly PostgresFixture _postgres;

    public HrAcknowledgementTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Лист_заводится_по_приказу()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var sheet = await Сервис(db).CreateAsync(new CreateSheetRequest
        {
            HrOrderId = стенд.OrderId,
            RequireSignature = false,
            Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
        }, стенд.Actor);

        Assert.Equal(стенд.OrderId, sheet.HrOrderId);
        Assert.Null(sheet.DocumentId);
    }

    [Fact]
    public async Task Лист_по_документу_работает_как_прежде()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var sheet = await Сервис(db).CreateAsync(new CreateSheetRequest
        {
            DocumentId = стенд.DocumentId,
            RequireSignature = false,
            Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
        }, стенд.Actor);

        Assert.Equal(стенд.DocumentId, sheet.DocumentId);
        Assert.Null(sheet.HrOrderId);
    }

    [Fact]
    public async Task Источник_указывается_ровно_один()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        // Ни одного источника — непонятно, с чем знакомят.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => сервис.CreateAsync(new CreateSheetRequest
            {
                Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
            }, стенд.Actor));

        // Оба сразу — тоже непонятно.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => сервис.CreateAsync(new CreateSheetRequest
            {
                DocumentId = стенд.DocumentId,
                HrOrderId = стенд.OrderId,
                Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
            }, стенд.Actor));
    }

    [Fact]
    public async Task Ознакомление_с_приказом_фиксируется_без_подписи()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = Сервис(db);

        var sheet = await сервис.CreateAsync(new CreateSheetRequest
        {
            HrOrderId = стенд.OrderId,
            RequireSignature = true,
            Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
        }, стенд.Actor);

        var entry = await db.Set<AcknowledgementEntry>()
            .FirstAsync(e => e.SheetId == sheet.Id && e.UserId == стенд.Employee);

        // Подписывать нечего: приказ вне единой карточки. Ознакомление остаётся
        // отметкой — кто и когда её поставил.
        await сервис.AcknowledgeAsync(entry.Id, стенд.Employee);

        var после = await db.Set<AcknowledgementEntry>().FirstAsync(e => e.Id == entry.Id);

        Assert.Equal(AcknowledgementState.Acknowledged, после.State);
        Assert.Null(после.SignatureId);
        Assert.NotNull(после.RespondedAt);
    }

    [Fact]
    public async Task Несуществующий_приказ_листа_не_получает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Сервис(db).CreateAsync(new CreateSheetRequest
            {
                HrOrderId = 999_999,
                Targets = new AcknowledgementTargets {UserIds = [стенд.Employee]},
            }, стенд.Actor));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int OrderId, int DocumentId, int Employee, int Actor);

    private static IAcknowledgementService Сервис(DelosferaDbContext db) =>
        new AcknowledgementService(
            db, new FakeSignatures(), new SilentNotifications(), new AuditService(db),
            NullLogger<AcknowledgementService>.Instance);

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var кадровик = await ПользовательАsync(db, "Кадровик");
        var сотрудник = await ПользовательАsync(db, "Сотрудник приказа");

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка",
            StatusCode = "Registered",
            AuthorId = кадровик,
        };
        db.Documents.Add(document);

        var order = new HrOrder
        {
            Kind = HrOrderKind.BusinessTrip,
            Status = HrOrderStatus.Signed,
            Title = "О направлении в командировку",
            Year = 2026,
            RegNumber = "1-лс",
            CreatedByUserId = кадровик,
        };
        db.Set<HrOrder>().Add(order);
        await db.SaveChangesAsync();

        return new Стенд(order.Id, document.Id, сотрудник, кадровик);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"hr-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
