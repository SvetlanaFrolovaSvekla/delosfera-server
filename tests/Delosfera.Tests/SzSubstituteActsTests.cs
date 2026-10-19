using delosfera_server.Common.Services;
using delosfera_server.Data;
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
/// Замещающий (GEN-14) действует по служебным запискам за отсутствующего.
///
/// Реестр задач показывал замещающему задачи отсутствующего — визу, решение адресата,
/// поручение, — но карточка записки отвечала «нет доступа», а решение и отчёт
/// принимались только от самого отсутствующего. Работа стояла до выхода из отпуска.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzSubstituteActsTests
{
    private readonly PostgresFixture _postgres;

    public SzSubstituteActsTests(PostgresFixture postgres) => _postgres = postgres;

    private static DateOnly Today => WorkingCalendar.Today;

    [Fact]
    public async Task Замещающий_адресата_видит_записку_и_выносит_решение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);
        var (szId, addresseeId, _) = await SeedOnAddresseeDecisionAsync(db, service, engine);
        var deputy = await AddUserAsync(db, "Замещающий адресата");

        // До оформления замещения записка ему не видна и решать её он не вправе.
        Assert.False(await SzVisibility.CanViewAsync(db, szId, deputy.Id, canSeeAll: false, canSeeOthersDrafts: false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DecideAsAddresseeAsync(szId, "Согласен", deputy.Id));

        await AddSubstitutionAsync(db, addresseeId, deputy.Id, Today.AddDays(-1), Today.AddDays(5));

        Assert.True(await SzVisibility.CanViewAsync(db, szId, deputy.Id, canSeeAll: false, canSeeOthersDrafts: false));

        var decided = await service.DecideAsAddresseeAsync(szId, "Согласен, прошу исполнить", deputy.Id);

        Assert.Equal(SzStatus.OnExecution, decided.StatusCode);
        var stored = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);
        // В деле остаётся, кто именно вынес решение — замещающий, а не отсутствующий.
        Assert.Equal(deputy.Id, stored.AddresseeDecisionByUserId);
    }

    [Fact]
    public async Task Закончившееся_или_отменённое_замещение_прав_не_даёт()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);
        var (szId, addresseeId, _) = await SeedOnAddresseeDecisionAsync(db, service, engine);
        var formerDeputy = await AddUserAsync(db, "Замещал до вчера");
        var cancelledDeputy = await AddUserAsync(db, "Замещение отменено");

        await AddSubstitutionAsync(db, addresseeId, formerDeputy.Id, Today.AddDays(-10), Today.AddDays(-1));
        await AddSubstitutionAsync(db, addresseeId, cancelledDeputy.Id, Today, Today.AddDays(3), cancelled: true);

        foreach (var outsider in new[] {formerDeputy.Id, cancelledDeputy.Id})
        {
            Assert.False(await SzVisibility.CanViewAsync(db, szId, outsider, canSeeAll: false, canSeeOthersDrafts: false));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.DecideAsAddresseeAsync(szId, "Согласен", outsider));
        }
    }

    [Fact]
    public async Task Замещающий_исполнителя_видит_поручение_и_отчитывается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);
        var execution = NewExecution(db);
        var (szId, addresseeId, _) = await SeedOnAddresseeDecisionAsync(db, service, engine);
        var performer = await AddUserAsync(db, "Исполнитель в отпуске");
        var deputy = await AddUserAsync(db, "Замещающий исполнителя");

        await service.DecideAsAddresseeAsync(szId, "Поручаю", addresseeId);
        var assignment = Assert.Single(await execution.ResolveAsync(szId, new SzResolutionRequest
        {
            Text = "Поручаю",
            Assignments = [new SzAssignmentRequest {AssigneeUserId = performer.Id, Text = "Подготовить расчёт", IsPrimary = true}],
        }, addresseeId));

        // Без замещения чужой отчёт не принимается.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => execution.ReportAsync(assignment.Id, "Расчёт", deputy.Id));

        await AddSubstitutionAsync(db, performer.Id, deputy.Id, Today, Today.AddDays(7));

        // «Мои поручения» и счётчик вкладки — с поручением отсутствующего.
        Assert.Contains(await execution.MyAsync(deputy.Id), a => a.Id == assignment.Id);
        Assert.Equal(1, await execution.CountMyAsync(deputy.Id));

        var reported = await execution.ReportAsync(assignment.Id, "Расчёт приложен", deputy.Id);
        Assert.Equal(nameof(SzAssignmentState.Reported), reported.State);
    }

    [Fact]
    public async Task Записка_согласующего_в_отпуске_есть_в_списке_согласую_я_у_замещающего()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, _) = NewService(db);
        var (szId, approverId) = await SeedOnApprovalAsync(db, service);
        var deputy = await AddUserAsync(db, "Замещающий согласующего");

        Assert.Equal(0, await service.InboxCountAsync(deputy.Id));

        await AddSubstitutionAsync(db, approverId, deputy.Id, Today, Today.AddDays(2));

        var inbox = await service.InboxAsync(deputy.Id, 1, 25);
        Assert.Contains(inbox.Items, i => i.Id == szId);
        Assert.Equal(1, await service.InboxCountAsync(deputy.Id));
        Assert.True(await SzVisibility.CanViewAsync(db, szId, deputy.Id, canSeeAll: false, canSeeOthersDrafts: false));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task AddSubstitutionAsync(
        DelosferaDbContext db, int absentUserId, int substituteUserId, DateOnly from, DateOnly to, bool cancelled = false)
    {
        db.Substitutions.Add(new Substitution
        {
            UserId = absentUserId,
            SubstituteUserId = substituteUserId,
            StartsOn = from,
            EndsOn = to,
            Reason = "Отпуск",
            IsCancelled = cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static SzExecutionService NewExecution(DelosferaDbContext db) =>
        // Без права «видеть все записки»: проверяем именно замещение, а не привилегию.
        new(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)),
            new AuditService(db), new FakeCurrentUser(0), new BankClock());

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

    /// <summary>Записка зарегистрирована и стоит у согласующего.</summary>
    private static async Task<(int SzId, int ApproverId)> SeedOnApprovalAsync(DelosferaDbContext db, ISzService service)
    {
        var author = await AddUserAsync(db, "Автор записки");
        var addressee = await AddUserAsync(db, "Адресат записки");
        var approver = await AddUserAsync(db, "Согласующий записки");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка к замещению",
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = addressee.Id,
            ApproverUserIds = [approver.Id],
        }, author.Id);

        await service.SubmitAsync(draft.Id, author.Id);
        var registered = await service.RegisterAsync(draft.Id, author.Id);
        Assert.Equal(SzStatus.OnApproval, registered.StatusCode);

        return (draft.Id, approver.Id);
    }

    /// <summary>Согласование пройдено, записка ждёт решения адресата (как в SzAddresseeDecisionTests).</summary>
    private static async Task<(int SzId, int AddresseeId, int ApproverId)> SeedOnAddresseeDecisionAsync(
        DelosferaDbContext db, ISzService service, IRouteEngine engine)
    {
        var (szId, approverId) = await SeedOnApprovalAsync(db, service);

        var sz = await db.SzDocuments.Include(x => x.Document).SingleAsync(x => x.Id == szId);
        var participant = await db.RouteParticipants
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId
                        && p.RouteStep.Kind == StepKind.Approval)
            .OrderBy(p => p.Id)
            .FirstAsync();
        await engine.ResolveAsync(participant.Id, ResolutionType.Approved, null, approverId);

        // Доводим до «у адресата» напрямую — так же, как соседние тесты решения адресата.
        await db.Entry(sz).ReloadAsync();
        await db.Entry(sz.Document!).ReloadAsync();
        sz.Document!.StatusCode = SzStatus.OnAddresseeDecision;
        await db.SaveChangesAsync();

        return (szId, sz.AddresseeUserId!.Value, approverId);
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-deputy-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
