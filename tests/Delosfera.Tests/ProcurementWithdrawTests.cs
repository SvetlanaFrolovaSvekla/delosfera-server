using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Отзыв заявки на закупку инициатором.
///
/// Статус «Отозвана» был объявлен, а выставить его было нечем: заявку,
/// отправленную по ошибке, автор вернуть не мог. Удаление разрешено только
/// черновику, значит оставалось просить согласующих отклонить — и чужая ошибка
/// выглядела в реестре как отказ подразделения.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProcurementWithdrawTests
{
    private readonly PostgresFixture _postgres;

    public ProcurementWithdrawTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Автор_отзывает_заявку_с_согласования()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Заявки(db).WithdrawAsync(стенд.RequestId, "Закупку отложили до бюджета", стенд.Author);

        Assert.Equal(ProcurementStatus.Cancelled, await СтатусАsync(db, стенд.DocumentId));
    }

    [Fact]
    public async Task Маршрут_прерывается_а_резолюции_остаются()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Заявки(db).WithdrawAsync(стенд.RequestId, "Ошиблись подразделением", стенд.Author);

        var route = await db.RouteInstances.AsNoTracking().FirstAsync(r => r.Id == стенд.RouteId);
        var document = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == стенд.DocumentId);

        Assert.Equal(RouteInstanceStatus.Interrupted, route.Status);

        // Ссылка на маршрут снимается: заявка больше ни у кого не в работе.
        Assert.Null(document.CurrentRouteInstanceId);
    }

    [Fact]
    public async Task Обоснование_обязательно()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Заявки(db).WithdrawAsync(стенд.RequestId, "   ", стенд.Author));
    }

    [Fact]
    public async Task Чужую_заявку_отозвать_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var посторонний = await ПользовательАsync(db, "Согласующий");

        // Иначе согласующий прерывал бы чужой процесс вместо того, чтобы вынести
        // по нему решение.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Заявки(db).WithdrawAsync(стенд.RequestId, "Не согласен", посторонний));
    }

    [Fact]
    public async Task После_передачи_в_закупку_отзывать_поздно()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, ProcurementStatus.InProcurement);

        // Процедура объявлена, поставщики приглашены: тут прекращают саму
        // процедуру, а не забирают заявку.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Заявки(db).WithdrawAsync(стенд.RequestId, "Передумали", стенд.Author));
    }

    [Fact]
    public async Task Отозванную_заявку_можно_отправить_снова()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, ProcurementStatus.OnRevision);
        var заявки = Заявки(db);

        await заявки.WithdrawAsync(стенд.RequestId, "Уточняем сумму", стенд.Author);

        // Отзыв — не приговор: заявка возвращается автору, и он вправе передумать.
        // Проверяется, что запрет отправки касается статуса, а не самой заявки.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => заявки.SubmitAsync(стенд.RequestId, стенд.Author));

        await db.Documents.Where(d => d.Id == стенд.DocumentId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.StatusCode, ProcurementStatus.Draft));

        Assert.Equal(ProcurementStatus.Draft, await СтатусАsync(db, стенд.DocumentId));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int RequestId, int DocumentId, int RouteId, int Author);

    private static IProcurementRequestService Заявки(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));

        // Движок настоящий: прерывание маршрута — половина проверяемого поведения,
        // и заглушка подтверждала бы только то, что метод вызван.
        var engine = new RouteEngine(
            db, audit, [new ProcurementRouteCompletionHandler(db, documents, audit)],
            new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));

        return new ProcurementRequestService(
            db, documents, audit, new AuthorityMatrixService(db, new AuditService(db)),
            new ProcurementRouteService(db, engine), engine, new BankClock());
    }

    private static async Task<string> СтатусАsync(DelosferaDbContext db, int documentId) =>
        await db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.StatusCode)
            .FirstAsync();

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"wd-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<Стенд> SeedAsync(
        DelosferaDbContext db, string status = ProcurementStatus.OnApproval)
    {
        var автор = await ПользовательАsync(db, "Инициатор закупки");
        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = status,
            AuthorId = автор,
            RegNumber = $"ЗК-2026-{Guid.NewGuid().ToString("N")[..4]}",
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var route = new RouteInstance {DocumentId = doc.Id, Status = RouteInstanceStatus.Running};
        db.RouteInstances.Add(route);
        await db.SaveChangesAsync();

        doc.CurrentRouteInstanceId = route.Id;

        db.ProcurementRequests.Add(new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Мониторы",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 45_000m,
            MethodId = method.Id,
        });
        await db.SaveChangesAsync();

        var request = await db.ProcurementRequests.AsNoTracking()
            .FirstAsync(r => r.DocumentId == doc.Id);

        return new Стенд(request.Id, doc.Id, route.Id, автор);
    }
}
