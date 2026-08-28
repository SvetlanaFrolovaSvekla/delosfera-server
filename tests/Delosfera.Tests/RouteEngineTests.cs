using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Движок согласования: автоакцепт по нормативу (TID-08), приоритет строгого режима
/// над автоакцептом (TID-10) и требование подписи нужного уровня на этапе (SIG-04).
///
/// Автоакцепт — единственное место, где система принимает решение за человека.
/// Ошибка здесь означает документ, «согласованный» молчанием там, где согласования
/// не было.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RouteEngineTests
{
    private readonly PostgresFixture _postgres;

    public RouteEngineTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task OverdueParticipant_GetsAutoAccept()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (instanceId, participantId) = await SeedActiveRouteAsync(db, overdue: true);

        await engine.ApplyOverdueAsync(DateTime.UtcNow);

        var participant = await db.RouteParticipants.AsNoTracking()
            .Include(p => p.Resolution)
            .SingleAsync(p => p.Id == participantId);

        Assert.Equal(ParticipantState.Done, participant.State);
        Assert.Equal(ResolutionType.AutoAccept, participant.Resolution!.Type);
    }

    [Fact]
    public async Task OverdueParticipant_OnRouteReturnedForRevision_IsNotAutoAccepted()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (instanceId, participantId) = await SeedActiveRouteAsync(db, overdue: true);

        // Маршрут вернули на доработку: документ ждёт правок инициатора, и молчание
        // согласующего здесь не согласие (TID-10).
        var instance = await db.RouteInstances.SingleAsync(i => i.Id == instanceId);
        instance.Status = RouteInstanceStatus.OnRevision;
        await db.SaveChangesAsync();

        await engine.ApplyOverdueAsync(DateTime.UtcNow);

        var participant = await db.RouteParticipants.AsNoTracking()
            .Include(p => p.Resolution)
            .SingleAsync(p => p.Id == participantId);

        Assert.Equal(ParticipantState.Active, participant.State);
        Assert.Null(participant.Resolution);
    }

    [Fact]
    public async Task OverdueParticipant_WithOpenRemarkOnStep_IsNotAutoAccepted()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (_, participantId) = await SeedActiveRouteAsync(db, overdue: true, withOpenRemark: true);

        await engine.ApplyOverdueAsync(DateTime.UtcNow);

        var participant = await db.RouteParticipants.AsNoTracking()
            .SingleAsync(p => p.Id == participantId);

        Assert.Equal(ParticipantState.Active, participant.State);
    }

    [Fact]
    public async Task OverdueOnFinalMethodologyStep_IsEscalatedNotAutoAccepted()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (_, participantId) = await SeedActiveRouteAsync(db, overdue: true, isFinalMethodology: true);

        await engine.ApplyOverdueAsync(DateTime.UtcNow);

        var participant = await db.RouteParticipants.AsNoTracking()
            .Include(p => p.Resolution)
            .SingleAsync(p => p.Id == participantId);

        // Финальный контроль методологии не «досогласовывается» сам: он эскалируется.
        Assert.Equal(ParticipantState.Active, participant.State);
        Assert.Null(participant.Resolution);

        var task = await db.WorkflowTasks.AsNoTracking()
            .SingleAsync(t => t.RouteParticipantId == participantId);

        Assert.Equal(WorkflowTaskState.Escalated, task.State);
    }

    [Fact]
    public async Task NotYetDueParticipant_IsUntouched()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (_, participantId) = await SeedActiveRouteAsync(db, overdue: false);

        await engine.ApplyOverdueAsync(DateTime.UtcNow);

        var participant = await db.RouteParticipants.AsNoTracking()
            .SingleAsync(p => p.Id == participantId);

        Assert.Equal(ParticipantState.Active, participant.State);
    }

    [Fact]
    public async Task StepRequiringQualifiedSignature_RejectsResolutionWithoutIt()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (_, participantId) = await SeedActiveRouteAsync(
            db, overdue: false, requiredLevel: SignatureLevel.Qualified);

        var userId = (await db.RouteParticipants.AsNoTracking()
            .SingleAsync(p => p.Id == participantId)).UserId!.Value;

        // Без подписи вовсе.
        var withoutSignature = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ResolveAsync(participantId, ResolutionType.Approved, null, userId));

        Assert.Contains("подпись не приложена", withoutSignature.Message);

        // Простая подпись не закрывает этап, требующий квалифицированную.
        var simple = new Signature
        {
            DocumentAttachmentId = 1,
            UserId = userId,
            Level = SignatureLevel.Simple,
            At = DateTime.UtcNow,
        };

        db.Signatures.Add(simple);
        await db.SaveChangesAsync();

        var wrongLevel = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ResolveAsync(participantId, ResolutionType.Approved, null, userId, simple.Id));

        Assert.Contains("приложена простая подпись", wrongLevel.Message);
    }

    [Fact]
    public async Task StepRequiringSignature_StillAllowsRejectionWithoutIt()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var engine = NewEngine(db);

        var (_, participantId) = await SeedActiveRouteAsync(
            db, overdue: false, requiredLevel: SignatureLevel.Qualified);

        var userId = (await db.RouteParticipants.AsNoTracking()
            .SingleAsync(p => p.Id == participantId)).UserId!.Value;

        // Отклонение ничего не удостоверяет: требовать под ним ЭЦП значит мешать
        // остановить процесс.
        await engine.ResolveAsync(participantId, ResolutionType.Rejected, "Нет обоснования цены", userId);

        var participant = await db.RouteParticipants.AsNoTracking()
            .Include(p => p.Resolution)
            .SingleAsync(p => p.Id == participantId);

        Assert.Equal(ResolutionType.Rejected, participant.Resolution!.Type);
    }

    /// <summary>
    /// Маршрут подписания строится без согласующих.
    ///
    /// В служебных записках подписание отделено от согласования: визируют, потом
    /// регистрируют, и только потом подписывают. Маршрут подписания строили общим
    /// методом с пустым списком согласующих — тот отказывал, и регистрация падала
    /// с «Не выбран ни один согласующий», а человек видел «Не удалось
    /// зарегистрировать записку» без объяснения.
    /// </summary>
    [Fact]
    public async Task InstantiateForSigner_BuildsSigningStepWithoutApprovers()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var user = new User
        {
            FullName = "Подписант записки",
            Email = $"signer-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка на подпись",
            StatusCode = "PendingRegistration",
            AuthorId = user.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var engine = NewEngine(db);
        var instance = await engine.InstantiateForSignerAsync(document.Id, user.Id);

        var step = Assert.Single(await db.RouteSteps
            .Include(x => x.Participants)
            .Where(x => x.RouteInstanceId == instance.Id)
            .ToListAsync());

        Assert.Equal(StepKind.Signing, step.Kind);
        Assert.Equal(user.Id, Assert.Single(step.Participants).UserId);

        // Запуск делает подписанта активным: иначе записка ждёт молча, и в его
        // задачах она не появляется.
        await engine.StartAsync(instance.Id, user.Id);

        var participant = await db.RouteParticipants
            .SingleAsync(p => p.RouteStep!.RouteInstanceId == instance.Id);

        Assert.Equal(ParticipantState.Active, participant.State);
    }

    /// <summary>
    /// Маршрут согласования без согласующих по-прежнему отвергается: отказ верный,
    /// ошибка была в том, что этим методом строили подписание.
    /// </summary>
    [Fact]
    public async Task InstantiateForApprovers_WithoutApprovers_IsRefused()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var user = new User
        {
            FullName = "Подписант записки",
            Email = $"signer-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка без согласующих",
            StatusCode = "PendingRegistration",
            AuthorId = user.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewEngine(db).InstantiateForApproversAsync(
                document.Id, approverUserIds: [], parallel: false, signerUserId: user.Id));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static RouteEngine NewEngine(DelosferaDbContext db) =>
        new(db, new AuditService(db), [], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures());

    /// <summary>
    /// Маршрут из одного этапа с одним активным согласующим. Срок ставится в прошлое
    /// или в будущее — от этого зависит, сработает ли автоакцепт.
    /// </summary>
    private static async Task<(int InstanceId, int ParticipantId)> SeedActiveRouteAsync(
        DelosferaDbContext db,
        bool overdue,
        bool withOpenRemark = false,
        bool isFinalMethodology = false,
        SignatureLevel? requiredLevel = null)
    {
        var user = new User
        {
            FullName = "Согласующий",
            Email = $"approver-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Документ для проверки движка",
            StatusCode = "OnApproval",
            AuthorId = user.Id,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var participant = new RouteParticipant
        {
            UserId = user.Id,
            State = ParticipantState.Active,
            ActivatedAt = DateTime.UtcNow.AddHours(-5),
            DueAt = overdue ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(24),
            Required = true,
        };

        var instance = new RouteInstance
        {
            DocumentId = document.Id,
            Status = RouteInstanceStatus.Running,
            CurrentStepOrder = 1,
            StartedAt = DateTime.UtcNow.AddHours(-5),
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Approval,
                    IsFinalMethodology = isFinalMethodology,
                    TimeNormHours = 4,
                    ActivatedAt = DateTime.UtcNow.AddHours(-5),
                    RequiredSignatureLevel = requiredLevel,
                    Participants = [participant],
                },
            ],
        };

        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();

        // Задача создаётся после сохранения: у участника появляется идентификатор,
        // а связь у задачи — по нему, без навигационного свойства.
        db.WorkflowTasks.Add(new WorkflowTask
        {
            RouteParticipantId = participant.Id,
            AssigneeUserId = user.Id,
            Type = "Approval",
            DueAt = participant.DueAt,
            State = WorkflowTaskState.Open,
            CreatedAt = DateTime.UtcNow.AddHours(-5),
        });

        await db.SaveChangesAsync();

        if (withOpenRemark)
        {
            // Замечание оставил другой участник того же этапа: строгий режим держит
            // весь этап, а не только автора замечания.
            var other = new RouteParticipant
            {
                RouteStepId = instance.Steps.First().Id,
                UserId = user.Id,
                State = ParticipantState.Done,
                Required = true,
            };

            db.RouteParticipants.Add(other);
            await db.SaveChangesAsync();

            var resolution = new Resolution
            {
                RouteParticipantId = other.Id,
                Type = ResolutionType.ApprovedWithRemarks,
                Comment = "Уточнить формулировку пункта 3",
                At = DateTime.UtcNow.AddHours(-2),
            };

            db.Resolutions.Add(resolution);
            await db.SaveChangesAsync();

            db.Remarks.Add(new Remark
            {
                ResolutionId = resolution.Id,
                Text = "Уточнить формулировку пункта 3",
                State = RemarkState.Open,
            });

            await db.SaveChangesAsync();
        }

        return (instance.Id, participant.Id);
    }

    /// <summary>Замещений нет: проверяется движок, а не подмена согласующего.</summary>
    private sealed class NoSubstitutions : ISubstitutionService
    {
        public Task<List<SubstitutionDto>> ListAsync(int? userId) => Task.FromResult(new List<SubstitutionDto>());

        public Task<SubstitutionDto> CreateAsync(SubstitutionCreateRequest request, int actorUserId) =>
            throw new NotSupportedException();

        public Task<SubstitutionDto> CancelAsync(int id, int actorUserId) => throw new NotSupportedException();

        public Task<List<int>> GetActingForUserIdsAsync(int substituteUserId) =>
            Task.FromResult(new List<int>());
    }

    /// <summary>Уведомления в этих проверках не участвуют — важны переходы состояний.</summary>
    private sealed class SilentNotifier : IWorkflowNotifier
    {
        public Task TaskAssignedAsync(IEnumerable<int> participantIds) => Task.CompletedTask;
        public Task OverdueAsync(int participantId, bool escalated) => Task.CompletedTask;

        public Task RouteFinishedAsync(int routeInstanceId, RouteInstanceStatus status, string? comment) =>
            Task.CompletedTask;
    }
}
