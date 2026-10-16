using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Куда идёт записка после подписи и кто видит её поручения.
///
/// Когда подписант назван отдельно от адресата, записка после подписи уходила
/// сразу на исполнение: адресат не получал ни задачи, ни уведомления, и решение
/// по существу за него не выносил никто. Поручения же по любой записке отдавались
/// любому вошедшему по одному {id}.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzSeparateSignerTests
{
    private readonly PostgresFixture _postgres;

    public SzSeparateSignerTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Подписал_отдельный_подписант__записка_ждёт_решения_адресата()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var адресат = await AddUserAsync(db, "Адресат");
        var подписант = await AddUserAsync(db, "Подписант");
        var szId = await ПровестиДоПодписиАsync(db, service, engine, адресат.Id, подписант.Id);

        var sz = await db.SzDocuments.AsNoTracking().Include(x => x.Document).SingleAsync(x => x.Id == szId);
        Assert.Equal(SzStatus.OnAddresseeDecision, sz.Document!.StatusCode);

        // Задача у адресата — в списке задач, а не только в уведомлениях.
        Assert.True(await db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.AssigneeUserId == адресат.Id
            && t.Type == SzRouteCompletionHandler.AddresseeDecisionTask
            && t.State == WorkflowTaskState.Open));

        // Адресат выносит решение — записка уходит на исполнение, задача закрыта.
        var decided = await service.DecideAsAddresseeAsync(szId, "Согласен, прошу исполнить", адресат.Id);
        Assert.Equal(SzStatus.OnExecution, decided.StatusCode);
        Assert.False(await db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.Type == SzRouteCompletionHandler.AddresseeDecisionTask
            && t.State == WorkflowTaskState.Open));
    }

    [Fact]
    public async Task Подписал_сам_адресат__записка_сразу_на_исполнении()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var адресат = await AddUserAsync(db, "Адресат-подписант");
        var szId = await ПровестиДоПодписиАsync(db, service, engine, адресат.Id, signerId: null);

        var sz = await db.SzDocuments.AsNoTracking().Include(x => x.Document).SingleAsync(x => x.Id == szId);
        Assert.Equal(SzStatus.OnExecution, sz.Document!.StatusCode);

        // Отдельного шага нет: подпись и резолюция — действия одного человека.
        Assert.False(await db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId && t.Type == SzRouteCompletionHandler.AddresseeDecisionTask));
    }

    [Fact]
    public async Task Поручения_видит_только_тот_кому_видна_записка()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var автор = await AddUserAsync(db, "Автор");
        var исполнитель = await AddUserAsync(db, "Исполнитель");
        var посторонний = await AddUserAsync(db, "Посторонний");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Sz, Title = "Записка об окладах",
            StatusCode = SzStatus.OnExecution, AuthorId = автор.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument {DocumentId = document.Id, KindId = kind.Id, Body = "Текст"};
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        db.SzAssignments.Add(new SzAssignment
        {
            SzDocumentId = sz.Id, AssigneeUserId = исполнитель.Id,
            Text = "Подготовить расчёт", CreatedByUserId = автор.Id,
        });
        await db.SaveChangesAsync();

        // Посторонний без права видеть все записки — отказ.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Execution(db, посторонний.Id).ListAsync(sz.Id, посторонний.Id));

        // Автор и исполнитель поручения — видят.
        Assert.Single(await Execution(db, автор.Id).ListAsync(sz.Id, автор.Id));
        Assert.Single(await Execution(db, исполнитель.Id).ListAsync(sz.Id, исполнитель.Id));

        // Держатель реестра целиком — тоже.
        Assert.Single(await Execution(db, посторонний.Id, PermissionCode.ViewAllSz).ListAsync(sz.Id, посторонний.Id));

        // Несуществующая записка — «не найдена», а не пустой список.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Execution(db, автор.Id).ListAsync(int.MaxValue, автор.Id));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    /// <summary>Записка: черновик → регистрация → согласование → подпись.</summary>
    private static async Task<int> ПровестиДоПодписиАsync(
        DelosferaDbContext db, ISzService service, IRouteEngine engine, int addresseeId, int? signerId)
    {
        var автор = await AddUserAsync(db, "Автор");
        var согласующий = await AddUserAsync(db, "Согласующий");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка с подписью",
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = addresseeId,
            SignerUserId = signerId,
            ApproverUserIds = [согласующий.Id],
        }, автор.Id);

        await service.SubmitAsync(draft.Id, автор.Id);
        await service.RegisterAsync(draft.Id, автор.Id);

        var documentId = (await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).DocumentId;

        var approval = await db.RouteParticipants
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == documentId && p.RouteStep.Kind == StepKind.Approval)
            .FirstAsync();
        await engine.ResolveAsync(approval.Id, ResolutionType.Approved, null, согласующий.Id);

        // Проверяем ход записки, а не саму подпись: требование ЭЦП на этапе снимаем.
        var signing = await db.RouteParticipants.Include(p => p.RouteStep)
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == documentId && p.RouteStep.Kind == StepKind.Signing)
            .SingleAsync();
        Assert.Equal(signerId ?? addresseeId, signing.UserId);
        signing.RouteStep!.RequiredSignatureLevel = null;
        await db.SaveChangesAsync();

        await engine.ResolveAsync(signing.Id, ResolutionType.Approved, null, signing.UserId!.Value);

        return draft.Id;
    }

    private static (ISzService Service, IRouteEngine Engine) NewService(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var signingProvider = new TestServiceProvider();
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications(), signingProvider);
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(),
            new FakeSignatures(), new RouteRoleResolver(db));
        signingProvider.Engine = engine;

        var currentUser = new FakeCurrentUser(0, PermissionCode.ViewAllSz);
        var procurement = new SzProcurementService(db, documents, audit, currentUser);

        return (new SzService(db, documents, audit, engine, new PassthroughHtml(), currentUser, handler,
            procurement, new RouteTemplateSelector(db), new BankClock()), engine);
    }

    private static SzExecutionService Execution(DelosferaDbContext db, int userId, params PermissionCode[] permissions) =>
        new(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)),
            new AuditService(db), new FakeCurrentUser(userId, permissions), new BankClock());

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
