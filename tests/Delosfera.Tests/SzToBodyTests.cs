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
/// Вынесение вопроса на коллегиальный орган.
///
/// Записку подписывает тот, кому она адресована, — подписант и адресат это одно
/// лицо. Подписав, он отписывает её исполнителям резолюцией; а если вправе —
/// Председатель Правления или исполняющий обязанности — может вынести вопрос
/// на коллегиальный орган.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzToBodyTests
{
    private readonly PostgresFixture _postgres;

    public SzToBodyTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Вопрос_выносится_на_орган_и_ждёт_секретаря()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, addresseeId, szId) = await SeedSignedAsync(db);

        var итог = await service.SubmitToBodyAsync(szId, new SzToBodyRequest
        {
            Body = MeetingBody.Board,
            Question = "О приобретении сервера резервного копирования",
        }, addresseeId);

        Assert.Equal(SzStatus.OnBoardReview, итог.StatusCode);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);

        // Записка появляется у секретаря в «Вопросах на рассмотрение»; на какое
        // заседание её вынести — решает он, а не система.
        Assert.Equal(MeetingBody.Board, sz.SubmitToBody);
        Assert.Equal("О приобретении сервера резервного копирования", sz.SubmitToBodyQuestion);
        Assert.Equal(addresseeId, sz.SubmitToBodyRequestedByUserId);
    }

    [Fact]
    public async Task Вопрос_выносит_только_тот_кому_записка_адресована()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, _, szId) = await SeedSignedAsync(db);

        var посторонний = await AddUserAsync(db, "Посторонний сотрудник");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SubmitToBodyAsync(szId, new SzToBodyRequest {Body = MeetingBody.Board}, посторонний.Id));
    }

    [Fact]
    public async Task До_подписания_вопрос_не_выносится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, addresseeId, szId) = await SeedSignedAsync(db, stopAtApproval: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitToBodyAsync(szId, new SzToBodyRequest {Body = MeetingBody.Board}, addresseeId));
    }

    [Fact]
    public async Task После_подписания_записка_на_исполнении()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (_, _, szId) = await SeedSignedAsync(db);

        var sz = await db.SzDocuments.AsNoTracking()
            .Include(x => x.Document)
            .SingleAsync(x => x.Id == szId);

        // Подписал — и сразу отписывает исполнителям резолюцией. Отдельного шага
        // между подписью и резолюцией нет: это одно действие одного человека.
        Assert.Equal(SzStatus.OnExecution, sz.Document!.StatusCode);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Записка, доведённая до исполнения: согласована, зарегистрирована, подписана
    /// адресатом. Со stopAtApproval — остановленная на согласовании.
    /// </summary>
    private static async Task<(ISzService Service, int AddresseeId, int SzId)> SeedSignedAsync(
        DelosferaDbContext db, bool procurementKind = false, bool stopAtApproval = false)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));
        var currentUser = new FakeCurrentUser(0, PermissionCode.ViewAllSz);
        var procurement = new SzProcurementService(db, documents, audit, currentUser);

        var service = new SzService(db, documents, audit, engine, new PassthroughHtml(),
            currentUser, handler, procurement,
            new delosfera_server.Modules.Workflow.Services.RouteTemplateSelector(db));

        var author = await AddUserAsync(db, "Автор записки");
        var approver = await AddUserAsync(db, "Согласующий записки");
        // Адресат он же подписант: одно лицо, а не два.
        var addressee = await AddUserAsync(db, "Адресат записки");

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
            ApproverUserIds = [approver.Id],
            Amount = procurementKind ? 250_000m : null,
            HasBudget = procurementKind ? true : null,
        }, author.Id);

        await service.SubmitAsync(draft.Id, author.Id);

        if (stopAtApproval) return (service, addressee.Id, draft.Id);

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

        await engine.ResolveAsync(signing.Id, ResolutionType.Approved, null, addressee.Id);

        return (service, addressee.Id, draft.Id);
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
