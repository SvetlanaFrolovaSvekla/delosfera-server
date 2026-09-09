using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Вскрытие конкурсных заявок.
///
/// Сектор закупок не смог вскрыть заявки: кнопка отказывала. Причина — вскрытие
/// запрещалось в сам день окончания срока приёма, только со следующего. Конкурс,
/// объявленный и закрытый одним днём, вскрыть было нельзя вовсе; комиссия,
/// собравшаяся в назначенный день, упиралась в отказ.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TenderOpenBidsTests
{
    private readonly PostgresFixture _postgres;

    public TenderOpenBidsTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Вскрытие_в_день_окончания_срока_проходит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var today = new DateOnly(2026, 9, 7);
        var стенд = await SeedAsync(db, deadline: today);

        var конкурс = await Сервис(db, today).OpenBidsAsync(стенд.TenderId, стенд.Actor);

        Assert.Equal(TenderStatus.Opened, конкурс.Status);
        Assert.Single(конкурс.Bids, b => b.IsAdmitted);
    }

    [Fact]
    public async Task Вскрытие_после_срока_проходит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, deadline: new DateOnly(2026, 9, 5));

        var конкурс = await Сервис(db, new DateOnly(2026, 9, 7)).OpenBidsAsync(стенд.TenderId, стенд.Actor);

        Assert.Equal(TenderStatus.Opened, конкурс.Status);
    }

    [Fact]
    public async Task Вскрытие_раньше_срока_не_проводится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, deadline: new DateOnly(2026, 9, 10));

        // Срок ещё не наступил — приём заявок не закрыт.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Сервис(db, new DateOnly(2026, 9, 7)).OpenBidsAsync(стенд.TenderId, стенд.Actor));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int TenderId, int Actor);

    private static ITenderService Сервис(DelosferaDbContext db, DateOnly today) =>
        new TenderService(db, new AuditService(db), new FixedClock(today), new delosfera_server.Modules.Documents.Services.NumeratorService(db));

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db, DateOnly deadline)
    {
        var автор = new User
        {
            FullName = "Организатор закупки",
            Email = $"tender-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        var поставщик = new Supplier {Title = "ОсОО Ромашка", Inn = "123"};
        db.Users.Add(автор);
        db.Suppliers.Add(поставщик);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync(m => m.RequiresPublication);

        var document = new Document
        {
            Type = DocumentType.Procurement, Title = "Конкурс на поставку",
            StatusCode = ProcurementStatus.InProcurement, AuthorId = автор.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = document.Id, Subject = "Оборудование",
            Amount = 5_000_000m, MethodId = method.Id,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        // Конкурс объявлен: публикация отмечена, срок приёма задан.
        var tender = new Tender
        {
            RequestId = request.Id,
            Status = TenderStatus.Published,
            PublishedOn = deadline.AddDays(-5),
            PublishedAt = "Объявление размещено на сайте банка",
            SubmissionDeadline = deadline,
            Bids =
            [
                new TenderBid
                {
                    SupplierId = поставщик.Id, Price = 4_800_000m,
                    SubmittedOn = deadline, IsLate = false,
                },
            ],
        };
        db.Set<Tender>().Add(tender);
        await db.SaveChangesAsync();

        return new Стенд(tender.Id, автор.Id);
    }

    /// <summary>Часы с заданным «сегодня»: вскрытие зависит от даты, её и фиксируем.</summary>
    private sealed class FixedClock : IBankClock
    {
        private readonly DateOnly _today;
        public FixedClock(DateOnly today) => _today = today;
        public DateOnly Today => _today;
        public DateTime Now => _today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
}
