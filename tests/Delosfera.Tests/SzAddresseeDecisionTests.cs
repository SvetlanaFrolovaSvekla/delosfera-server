using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.DTO.Response;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Решение адресата по служебной записке.
///
/// Это ответ по существу вопроса, а не виза согласующего: его выносит только тот,
/// кто указан в поле «Кому». Подмена автора решения обесценивает саму записку —
/// в деле останется резолюция, которую названный в ней человек не выносил.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzAddresseeDecisionTests
{
    private readonly PostgresFixture _postgres;

    public SzAddresseeDecisionTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Decision_ByAnyoneButAddressee_IsRefused()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var (szId, addresseeId, approverId) = await SeedApprovedAsync(db, service, engine);

        // Согласующий по этой же записке — и тот решение вынести не может.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DecideAsAddresseeAsync(szId, "Согласен", approverId));

        var untouched = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);
        Assert.Null(untouched.AddresseeDecision);

        // А сам адресат — может.
        var decided = await service.DecideAsAddresseeAsync(szId, "Согласен, прошу исполнить", addresseeId);

        Assert.Equal("Согласен, прошу исполнить", decided.AddresseeDecision);
        Assert.Equal(SzStatus.OnExecution, decided.StatusCode);
    }

    [Fact]
    public async Task Decision_BeforeApprovalFinished_IsRefused()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var (szId, addresseeId, _) = await SeedSubmittedAsync(db, service, engine);

        // Согласование ещё идёт: решение по существу выносится после него, иначе
        // визы собираются под запиской, которая уже решена.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DecideAsAddresseeAsync(szId, "Согласен", addresseeId));

        Assert.Contains("ещё не дошла до адресата", error.Message);
    }

    [Fact]
    public async Task EmptyDecision_IsRefused()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var (szId, addresseeId, _) = await SeedApprovedAsync(db, service, engine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DecideAsAddresseeAsync(szId, "   ", addresseeId));
    }

    [Fact]
    public async Task Addressee_GetsTaskInInbox_UntilDecisionIsMade()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var (szId, addresseeId, _) = await SeedApprovedAsync(db, service, engine);

        // Уведомления мало: по списку задач должно быть видно, что человек кому-то
        // должен ответ, иначе записка ждёт молча.
        var inbox = await NewInbox(db).GetAsync(addresseeId);

        // Записка доведена до «у адресата» напрямую, поэтому её маршрут ещё не
        // закрыт и своя задача у него тоже висит. Ищем именно задачу на решение.
        var task = Assert.Single(inbox.Tasks.Where(t => t.TaskType == "Решение по записке"));

        Assert.Equal("Решение по записке", task.TaskType);
        Assert.Null(task.ParticipantId);
        Assert.Null(task.StepOrder);

        await service.DecideAsAddresseeAsync(szId, "Согласен", addresseeId);

        var afterDecision = await NewInbox(db).GetAsync(addresseeId);
        Assert.DoesNotContain(afterDecision.Tasks, t => t.TaskType == "Решение по записке");
    }

    [Fact]
    public async Task Assignment_AppearsInAssigneeInbox_AndClosesOnReport()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);
        var execution = new SzExecutionService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)), new AuditService(db));

        var (szId, addresseeId, _) = await SeedApprovedAsync(db, service, engine);
        var performer = await AddUserAsync(db, "Исполнитель поручения");

        await service.DecideAsAddresseeAsync(szId, "Согласен, поручаю", addresseeId);

        var assignments = await execution.ResolveAsync(szId, new SzResolutionRequest
        {
            Text = "Согласен, поручаю",
            Assignments = [new SzAssignmentRequest {AssigneeUserId = performer.Id, Text = "Подготовить расчёт", IsPrimary = true}],
        }, addresseeId);

        var assignment = Assert.Single(assignments);

        var inbox = await NewInbox(db).GetAsync(performer.Id);
        var task = Assert.Single(inbox.Tasks);
        Assert.Equal("Поручение", task.TaskType);

        // Отчитался — поручение ушло из списка задач исполнителя.
        await execution.ReportAsync(assignment.Id, "Расчёт приложен", performer.Id);
        Assert.Empty((await NewInbox(db).GetAsync(performer.Id)).Tasks);

        // Вернули с приёмки — работа снова его, и задача снова видна.
        await execution.ReturnAsync(assignment.Id, "Расчёт неполный", addresseeId);
        Assert.Single((await NewInbox(db).GetAsync(performer.Id)).Tasks);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static TaskInboxService NewInbox(DelosferaDbContext db) =>
        new(db, new NoSubstitutions(), new BankClock());

    private static (ISzService Service, IRouteEngine Engine) NewService(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));

        // Обработчик контура обязателен: именно он переводит записку к адресату по
        // завершении маршрута. Без него проверялся бы движок, а не поведение системы.
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));

        // Право «видеть все записки» — чтобы проверка касалась движения записки,
        // а не видимости реестра.
        var currentUser = new FakeCurrentUser(0, PermissionCode.ViewAllSz);

        var procurement = new SzProcurementService(db, documents, audit);

        return (new SzService(db, documents, audit, engine, new PassthroughHtml(),
            currentUser, handler, procurement,
            new delosfera_server.Modules.Workflow.Services.RouteTemplateSelector(db)), engine);
    }

    /// <summary>
    /// Регистрация записки с подписантом.
    ///
    /// Подписание идёт отдельным маршрутом после регистрации, и строить его надо
    /// маршрутом подписанта, а не маршрутом согласующих с пустым их списком: тот
    /// отказывается, и регистрация падает с «Не выбран ни один согласующий».
    /// На стенде это выглядело как «Не удалось зарегистрировать записку».
    /// </summary>
    [Fact]
    public async Task Register_WithSigner_SendsMemoToSigning()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (service, engine) = NewService(db);

        var author = await AddUserAsync(db, "Автор записки");
        var approver = await AddUserAsync(db, "Согласующий записки");
        var signer = await AddUserAsync(db, "Подписант записки");

        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка с подписантом",
            KindId = kind.Id,
            Body = "Текст записки",
            CorrespondentUnitId = null,
            AddresseeUserId = signer.Id,
            ApproverUserIds = [approver.Id],
        }, author.Id);

        await service.SubmitAsync(draft.Id, author.Id);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == draft.Id);

        var participant = await db.RouteParticipants
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId)
            .OrderBy(p => p.Id)
            .FirstAsync();

        await engine.ResolveAsync(participant.Id, ResolutionType.Approved, null, approver.Id);

        var registered = await service.RegisterAsync(draft.Id, author.Id);

        // Номер присвоен, и записка ушла подписанту — а не упала на построении
        // маршрута и не проскочила подписание.
        Assert.NotNull(registered.RegNumber);
        Assert.Equal(SzStatus.OnSigning, registered.StatusCode);

        // Подписывает тот, кому записка адресована: подписант и адресат — одно лицо.

        var signingParticipant = await db.RouteParticipants
            .Include(p => p.RouteStep)
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId
                        && p.RouteStep.Kind == StepKind.Signing)
            .SingleAsync();

        Assert.Equal(signer.Id, signingParticipant.UserId);
    }

    /// <summary>Записка, отправленная на согласование: один согласующий, один адресат.
    /// Номера у неё ещё нет — он присваивается после согласования.</summary>
    private static async Task<(int SzId, int AddresseeId, int ApproverId)> SeedSubmittedAsync(
        DelosferaDbContext db, ISzService service, IRouteEngine engine)
    {
        var author = await AddUserAsync(db, "Автор записки");
        var addressee = await AddUserAsync(db, "Адресат записки");
        var approver = await AddUserAsync(db, "Согласующий записки");

        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка на решение адресата",
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = addressee.Id,
            ApproverUserIds = [approver.Id],
        }, author.Id);

        await service.SubmitAsync(draft.Id, author.Id);

        return (draft.Id, addressee.Id, approver.Id);
    }

    /// <summary>То же, но согласование уже пройдено — записка ждёт решения адресата.</summary>
    private static async Task<(int SzId, int AddresseeId, int ApproverId)> SeedApprovedAsync(
        DelosferaDbContext db, ISzService service, IRouteEngine engine)
    {
        var (szId, addresseeId, approverId) = await SeedSubmittedAsync(db, service, engine);

        var sz = await db.SzDocuments.AsNoTracking().SingleAsync(x => x.Id == szId);

        var participant = await db.RouteParticipants
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == sz.DocumentId)
            .OrderBy(p => p.Id)
            .FirstAsync();

        await engine.ResolveAsync(participant.Id, ResolutionType.Approved, null, approverId);

        // Согласование идёт до регистрации: согласованная записка ждёт номера,
        // а к адресату попадает уже зарегистрированной.
        var afterApproval = await db.SzDocuments.AsNoTracking()
            .Include(x => x.Document)
            .SingleAsync(x => x.Id == szId);

        Assert.Equal(SzStatus.PendingRegistration, afterApproval.Document!.StatusCode);

        await service.RegisterAsync(szId, approverId);

        var afterRegistration = await db.SzDocuments
            .Include(x => x.Document)
            .SingleAsync(x => x.Id == szId);

        Assert.NotNull(afterRegistration.Document!.RegNumber);

        // Записки прежнего порядка стоят в «у адресата»: тогда подписант и адресат
        // были разными людьми, и решение по существу выносилось отдельным шагом.
        // Метод остаётся ради них — доводим записку до этого состояния напрямую.
        afterRegistration.Document.StatusCode = SzStatus.OnAddresseeDecision;
        await db.SaveChangesAsync();

        var handler = new SzRouteCompletionHandler(
            db, new DocumentService(db, new AuditService(db), new NumeratorService(db)),
            new AuditService(db), new SilentNotifications());

        await handler.CreateAddresseeTaskAsync(afterRegistration);

        return (szId, addresseeId, approverId);
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-decision-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private sealed class NoSubstitutions : ISubstitutionService
    {
        public Task<List<SubstitutionDto>> ListAsync(int? userId) => Task.FromResult(new List<SubstitutionDto>());

        public Task<SubstitutionDto> CreateAsync(SubstitutionCreateRequest request, int actorUserId) =>
            throw new NotSupportedException();

        public Task<SubstitutionDto> CancelAsync(int id, int actorUserId) => throw new NotSupportedException();

        public Task<List<int>> GetActingForUserIdsAsync(int substituteUserId) =>
            Task.FromResult(new List<int>());
    }


    private sealed class SilentNotifier : IWorkflowNotifier
    {
        public Task TaskAssignedAsync(IEnumerable<int> participantIds) => Task.CompletedTask;
        public Task OverdueAsync(int participantId, bool escalated) => Task.CompletedTask;

        public Task RouteFinishedAsync(int routeInstanceId, RouteInstanceStatus status, string? comment) =>
            Task.CompletedTask;
    }
}
