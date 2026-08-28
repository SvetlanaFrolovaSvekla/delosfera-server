using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Решение подписанта о дальнейшем ходе записки.
///
/// Подпись говорит «с текстом согласен», но не говорит, что делать дальше.
/// Дальше записка расходится на три пути: вопрос выносится на коллегиальный
/// орган, потребность в закупке уходит в Сектор закупок, остальное идёт на
/// исполнение. Выбирает подписант — он последний, кто видел записку целиком.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzSignerDecisionTests
{
    private readonly PostgresFixture _postgres;

    public SzSignerDecisionTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Решение_на_орган_отправляет_записку_в_вопросы_на_рассмотрение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, signerId, szId) = await SeedSignedAsync(db);

        var итог = await service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
        {
            Route = SzSignerRoute.Board,
            Body = MeetingBody.Board,
            Subject = "О приобретении сервера резервного копирования",
        }, signerId);

        Assert.Equal(SzStatus.OnBoardReview, итог.StatusCode);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);

        // Записка появляется у секретаря в «Вопросах на рассмотрение»; в повестку
        // конкретного заседания её включает он — система за него не решает.
        Assert.Equal(MeetingBody.Board, sz.SubmitToBody);
        Assert.Equal("О приобретении сервера резервного копирования", sz.SubmitToBodyQuestion);
        Assert.Equal(signerId, sz.SubmitToBodyRequestedByUserId);
    }

    [Fact]
    public async Task Решение_на_орган_без_органа_отклоняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, signerId, szId) = await SeedSignedAsync(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
            {
                Route = SzSignerRoute.Board,
            }, signerId));
    }

    [Fact]
    public async Task Решение_в_закупки_заводит_заявку_и_связь_с_запиской()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, signerId, szId) = await SeedSignedAsync(db, procurementKind: true);

        var итог = await service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
        {
            Route = SzSignerRoute.Procurement,
            Subject = "Серверное оборудование",
        }, signerId);

        Assert.Equal(SzStatus.OnExecution, итог.StatusCode);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);

        var link = await db.DocumentLinks.AsNoTracking()
            .Include(l => l.ToDocument)
            .SingleAsync(l => l.FromDocumentId == sz.DocumentId
                              && l.LinkType == SzProcurementService.LinkType);

        // Связь важнее самой заявки: по ней видно, из какой потребности выросла
        // закупка, и обратно — чем закончилась записка.
        Assert.Equal("Серверное оборудование", link.ToDocument!.Title);
    }

    [Fact]
    public async Task Решение_на_исполнение_ведёт_к_адресату()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, signerId, szId) = await SeedSignedAsync(db);

        var итог = await service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
        {
            Route = SzSignerRoute.Execution,
        }, signerId);

        // У записки назван адресат, значит решение по существу выносит он.
        Assert.Equal(SzStatus.OnAddresseeDecision, итог.StatusCode);
    }

    [Fact]
    public async Task Решение_выносит_только_подписант()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, _, szId) = await SeedSignedAsync(db);

        var посторонний = await AddUserAsync(db, "Посторонний сотрудник");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
            {
                Route = SzSignerRoute.Execution,
            }, посторонний.Id));
    }

    [Fact]
    public async Task До_подписания_решение_не_выносится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, signerId, szId) = await SeedSignedAsync(db, stopAtApproval: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsSignerAsync(szId, new SzSignerDecisionRequest
            {
                Route = SzSignerRoute.Execution,
            }, signerId));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Записка, доведённая до решения подписанта: согласована, зарегистрирована,
    /// подписана. Со stopAtApproval — остановленная на согласовании.
    /// </summary>
    private static async Task<(ISzService Service, int SignerId, int SzId)> SeedSignedAsync(
        DelosferaDbContext db, bool procurementKind = false, bool stopAtApproval = false)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures());
        var currentUser = new FakeCurrentUser(0, PermissionCode.ViewAllSz);
        var procurement = new SzProcurementService(db, documents, audit);

        var service = new SzService(db, documents, audit, engine, new PassthroughHtml(),
            currentUser, handler, procurement);

        var author = await AddUserAsync(db, "Автор записки");
        var approver = await AddUserAsync(db, "Согласующий записки");
        var addressee = await AddUserAsync(db, "Адресат записки");
        var signer = await AddUserAsync(db, "Подписант записки");

        // Для закупочного пути нужен вид записки «на закупку»: закупочный контур
        // проверяет именно его, и на прочих видах отказывает.
        var kind = procurementKind
            ? await db.SzKinds.AsNoTracking().FirstAsync(k => k.FormKey == SzFormKey.Procurement)
            : await db.SzKinds.AsNoTracking().FirstAsync();

        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка на подпись",
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = addressee.Id,
            SignerUserId = signer.Id,
            ApproverUserIds = [approver.Id],
            Amount = procurementKind ? 250_000m : null,
            HasBudget = procurementKind ? true : null,
        }, author.Id);

        await service.SubmitAsync(draft.Id, author.Id);

        if (stopAtApproval) return (service, signer.Id, draft.Id);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == draft.Id);

        var approval = await db.RouteParticipants
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId)
            .OrderBy(p => p.Id)
            .FirstAsync();

        await engine.ResolveAsync(approval.Id, ResolutionType.Approved, null, approver.Id);
        await service.RegisterAsync(draft.Id, author.Id);

        var signing = await db.RouteParticipants
            .Include(p => p.RouteStep)
            .SingleAsync(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId
                              && p.RouteStep.Kind == StepKind.Signing);

        await engine.ResolveAsync(signing.Id, ResolutionType.Approved, null, signer.Id);

        return (service, signer.Id, draft.Id);
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-signer-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }
}
