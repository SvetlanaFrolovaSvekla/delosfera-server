using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Маршрут согласования служебной записки по списку согласующих, выбранному автором.
///
/// Различие «по очереди» и «параллельно» видно только на структуре маршрута: при
/// последовательном согласовании второй участник не должен получить задачу, пока
/// первый не ответил, — иначе очередь существует лишь на бумаге, а согласуют все
/// одновременно.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzApprovalRouteTests
{
    private readonly PostgresFixture _postgres;

    public SzApprovalRouteTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task SequentialApproval_ActivatesApproversOneByOne()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (documentId, approvers) = await SeedAsync(db, count: 3);

        var instance = await engine.InstantiateForApproversAsync(documentId, approvers, parallel: false);
        await engine.StartAsync(instance.Id, approvers[0]);

        var steps = await LoadStepsAsync(db, instance.Id);

        Assert.Equal(3, steps.Count);
        Assert.All(steps, s => Assert.Single(s.Participants));

        // Активен только первый: остальные ждут своей очереди.
        Assert.Equal(ParticipantState.Active, steps[0].Participants[0].State);
        Assert.All(steps.Skip(1), s => Assert.Equal(ParticipantState.Pending, s.Participants[0].State));
    }

    [Fact]
    public async Task ParallelApproval_ActivatesEveryApproverAtOnce()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (documentId, approvers) = await SeedAsync(db, count: 3);

        var instance = await engine.InstantiateForApproversAsync(documentId, approvers, parallel: true);
        await engine.StartAsync(instance.Id, approvers[0]);

        var steps = await LoadStepsAsync(db, instance.Id);

        var step = Assert.Single(steps);
        Assert.Equal(StepMode.Parallel, step.Mode);
        Assert.Equal(3, step.Participants.Count);
        Assert.All(step.Participants, p => Assert.Equal(ParticipantState.Active, p.State));
    }

    [Fact]
    public async Task SequentialApproval_MovesToNextApprover_AfterFirstApproves()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (documentId, approvers) = await SeedAsync(db, count: 2);

        var instance = await engine.InstantiateForApproversAsync(documentId, approvers, parallel: false);
        await engine.StartAsync(instance.Id, approvers[0]);

        var first = (await LoadStepsAsync(db, instance.Id))[0].Participants[0];
        await engine.ResolveAsync(first.Id, ResolutionType.Approved, null, approvers[0]);

        var steps = await LoadStepsAsync(db, instance.Id);

        Assert.Equal(ParticipantState.Done, steps[0].Participants[0].State);
        Assert.Equal(ParticipantState.Active, steps[1].Participants[0].State);
    }

    [Fact]
    public async Task Rejection_ByAnyApprover_StopsTheRoute()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (documentId, approvers) = await SeedAsync(db, count: 2);

        var instance = await engine.InstantiateForApproversAsync(documentId, approvers, parallel: false);
        await engine.StartAsync(instance.Id, approvers[0]);

        var first = (await LoadStepsAsync(db, instance.Id))[0].Participants[0];

        // Отказ первого согласующего — второй записку уже не увидит: обсуждать нечего,
        // пока автор не переработает её.
        await engine.ResolveAsync(first.Id, ResolutionType.Rejected, "Нет обоснования", approvers[0]);

        var stored = await db.RouteInstances.AsNoTracking().SingleAsync(i => i.Id == instance.Id);
        Assert.Equal(RouteInstanceStatus.Rejected, stored.Status);

        var steps = await LoadStepsAsync(db, instance.Id);
        Assert.NotEqual(ParticipantState.Active, steps[1].Participants[0].State);
    }

    [Fact]
    public async Task ApprovalWithRemarks_ReturnsRouteForRevision()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (documentId, approvers) = await SeedAsync(db, count: 2);

        var instance = await engine.InstantiateForApproversAsync(documentId, approvers, parallel: false);
        await engine.StartAsync(instance.Id, approvers[0]);

        var first = (await LoadStepsAsync(db, instance.Id))[0].Participants[0];

        // Возврат на доработку — не отказ: записка возвращается автору, но маршрут
        // сохраняется, чтобы после правки продолжить с того же места.
        await engine.ResolveAsync(
            first.Id, ResolutionType.ApprovedWithRemarks, "Уточнить сумму", approvers[0]);

        var stored = await db.RouteInstances.AsNoTracking().SingleAsync(i => i.Id == instance.Id);

        Assert.True(
            stored.Status is RouteInstanceStatus.OnRevision or RouteInstanceStatus.Running,
            $"После замечаний маршрут не может быть в состоянии «{stored.Status}»");
        Assert.NotEqual(RouteInstanceStatus.Approved, stored.Status);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    /// <summary>Этап маршрута с участниками в виде списка — по ним удобно проверять порядок.</summary>
    private sealed record StepView(int Order, StepMode Mode, List<RouteParticipant> Participants);

    private static async Task<List<StepView>> LoadStepsAsync(DelosferaDbContext db, int instanceId) =>
        await db.RouteSteps.AsNoTracking()
            .Where(s => s.RouteInstanceId == instanceId)
            .OrderBy(s => s.Order)
            .Select(s => new StepView(s.Order, s.Mode, s.Participants.OrderBy(p => p.Id).ToList()))
            .ToListAsync();

    private static RouteEngine NewEngine(DelosferaDbContext db) =>
        new(db, new AuditService(db), [], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures());

    /// <summary>Записка и заданное число согласующих.</summary>
    private static async Task<(int DocumentId, List<int> Approvers)> SeedAsync(
        DelosferaDbContext db, int count)
    {
        var users = Enumerable.Range(1, count).Select(i => new User
        {
            FullName = $"Согласующий {i}",
            Email = $"sz-approver-{i}-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        }).ToList();

        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка на согласование",
            StatusCode = "Registered",
            AuthorId = users[0].Id,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (document.Id, users.Select(u => u.Id).ToList());
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
