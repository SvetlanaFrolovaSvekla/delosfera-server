using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Резолюция адресата после подписания.
///
/// Записка доходит до адресата двумя путями: без подписания — прямо с
/// согласования, а когда адресат её же и подписывает — сразу на исполнение.
/// Проверка допускала только первый, и отписать исполнителей не мог тот, кто
/// записку подписал: то есть ровно тот, ради кого резолюция и существует.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzAddresseeResolutionTests
{
    private readonly PostgresFixture _postgres;

    public SzAddresseeResolutionTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Подписавший_адресат_отписывает_исполнителям()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (szId, адресат) = await ЗавестиАsync(db, SzStatus.OnExecution);

        var details = await Сервис(db, адресат)
            .DecideAsAddresseeAsync(szId, "Прошу подготовить расчёт", адресат);

        Assert.Equal("Прошу подготовить расчёт", details.AddresseeDecision);
    }

    [Fact]
    public async Task Адресат_без_подписания_решает_как_прежде()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (szId, адресат) = await ЗавестиАsync(db, SzStatus.OnAddresseeDecision);

        var details = await Сервис(db, адресат)
            .DecideAsAddresseeAsync(szId, "Согласен", адресат);

        Assert.Equal("Согласен", details.AddresseeDecision);
    }

    [Fact]
    public async Task До_согласования_решение_не_выносится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (szId, адресат) = await ЗавестиАsync(db, SzStatus.OnApproval);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Сервис(db, адресат).DecideAsAddresseeAsync(szId, "Рано", адресат));
    }

    [Fact]
    public async Task Дважды_решение_не_выносится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (szId, адресат) = await ЗавестиАsync(db, SzStatus.OnExecution);

        var service = Сервис(db, адресат);
        await service.DecideAsAddresseeAsync(szId, "Первое решение", адресат);

        // Записка остаётся на исполнении и после резолюции: без этой проверки
        // второе решение молча затёрло бы первое, по которому уже работают.
        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DecideAsAddresseeAsync(szId, "Другое решение", адресат));

        Assert.Contains("уже вынесено", ошибка.Message);
    }

    [Fact]
    public async Task Посторонний_решение_не_выносит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (szId, _) = await ЗавестиАsync(db, SzStatus.OnExecution);

        var посторонний = await ПользовательАsync(db, "Посторонний");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Сервис(db, посторонний).DecideAsAddresseeAsync(szId, "Моё решение", посторонний));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static ISzService Сервис(DelosferaDbContext db, int userId)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));

        return new SzService(db, documents, audit, engine, new PassthroughHtml(),
            new FakeCurrentUser(userId), handler, new SzProcurementService(db, documents, audit),
            new delosfera_server.Modules.Workflow.Services.RouteTemplateSelector(db));
    }

    private static async Task<(int SzId, int AddresseeId)> ЗавестиАsync(
        DelosferaDbContext db, string status)
    {
        var автор = await ПользовательАsync(db, "Автор записки");
        var адресат = await ПользовательАsync(db, "Адресат записки");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка на резолюцию",
            StatusCode = status,
            AuthorId = автор,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument
        {
            DocumentId = document.Id,
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = адресат,
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        return (sz.Id, адресат);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"resolution-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
