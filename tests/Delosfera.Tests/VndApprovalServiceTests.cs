using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

[Collection(PostgresCollection.Name)]
public class VndApprovalServiceTests
{
    private readonly PostgresFixture _postgres;

    public VndApprovalServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private const int Approver1 = 200;
    private const int Approver2 = 201;

    private const int Initiator = 100;

    private static VndApprovalService NewService(DelosferaDbContext db) =>
        new(db, new NoopFileStorage(), new NoopNotificationService(),
            new FakeCurrentUser(Approver1), NullLogger<VndApprovalService>.Instance,
            new FakeActivityLog(), new ApprovalSheetGenerator(),
            new FixedApprovalUnitResolver(db));

    // Двухэтапный процесс на первичной фазе: решение по одному этапу не завершает фазу,
    // поэтому изолируем логику DecideAsync без тяжёлого перехода фаз.
    private static VndApprovalProcess SeedTwoStageProcess(DelosferaDbContext db)
    {
        // LoadProcessForVndAsync ищет редакцию по VndId, затем процесс по RedactionId и делает
        // Include(Vnd). Связь process→Vnd обязательная, поэтому Include превращается во внутренний
        // JOIN — без строки VndDocument процесс отфильтровывается.
        //
        // Идентификаторы не задаём: на настоящей базе единица уже занята сидовым ВНД,
        // и жёсткий ключ ломается о первичный индекс.
        TestSupport.EnsureUser(db, Initiator);
        TestSupport.EnsureUser(db, Approver1);
        TestSupport.EnsureUser(db, Approver2);

        var vnd = TestSupport.SeedVnd(db, VndStatus.Review, Initiator);

        var file = TestSupport.SeedFile(db, Initiator);
        var redaction = new VndRedaction
        {
            VndId = vnd.Id,
            Number = 1,
            Code = $"Р-{Guid.NewGuid():N}"[..8],
            TitleRu = "Редакция для проверки согласования",
            OrganId = db.ApprovalBodies.OrderBy(x => x.Id).First().Id,
            DeveloperId = db.OrganizationUnits.OrderBy(x => x.Id).First().Id,
            SecrecyLevelId = db.SecurityLevels.OrderBy(x => x.Id).First().Id,
            TypeId = db.TypesVnd.OrderBy(x => x.Id).First().Id,
            DocFileRuId = file.Id,
        };
        db.VndRedactions.Add(redaction);
        db.SaveChanges();

        var process = new VndApprovalProcess
        {
            VndId = vnd.Id,
            RedactionId = redaction.Id,
            InitiatorUserId = Initiator,
            Status = ApprovalProcessStatus.Primary,
            PrimaryDeadlineMinutes = 60,
            RepeatDeadlineMinutes = 60,
            FinalHoldDeadlineMinutes = 60,
            PrimaryStartedAt = DateTime.UtcNow,
            Stages =
            [
                new VndApprovalStage { Order = 1, OrgUnitId = db.OrganizationUnits.OrderBy(x => x.Id).First().Id, ApproverUserId = Approver1, PrimaryDecision = ApprovalStageDecision.Pending },
                new VndApprovalStage { Order = 2, OrgUnitId = db.OrganizationUnits.OrderBy(x => x.Id).First().Id, ApproverUserId = Approver2, PrimaryDecision = ApprovalStageDecision.Pending },
            ]
        };
        db.VndApprovalProcesses.Add(process);
        db.SaveChanges();
        return process;
    }

    [Fact]
    public async Task Decide_Approve_RecordsDecisionOnStage()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var stageId = seeded.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(seeded.VndId, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1);

        var stage = await db.VndApprovalStages.SingleAsync(s => s.Id == stageId);
        Assert.Equal(ApprovalStageDecision.Approved, stage.PrimaryDecision);
        Assert.NotNull(stage.PrimaryDecidedAt);
    }

    [Fact]
    public async Task Decide_ByUserNotAssignedToStage_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var stageId = seeded.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DecideAsync(seeded.VndId, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, 999));
    }

    [Fact]
    public async Task Decide_RejectWithoutComment_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var stageId = seeded.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DecideAsync(seeded.VndId, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Reject }, Approver1));
    }

    [Fact]
    public async Task Decide_Twice_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var stageId = seeded.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(seeded.VndId, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DecideAsync(seeded.VndId, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1));
    }

    [Fact]
    public async Task Decide_UnknownStage_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var svc = NewService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.DecideAsync(seeded.VndId, 999999, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1));
    }

    [Fact]
    public async Task Cancel_ByInitiator_SetsCancelledAndRevertsToDraft()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var svc = NewService(db);

        await svc.CancelAsync(seeded.VndId, Initiator); // инициатор процесса

        var process = await db.VndApprovalProcesses.SingleAsync(x => x.Id == seeded.Id);
        var redaction = await db.VndRedactions.SingleAsync(x => x.Id == seeded.RedactionId);
        var vnd = await db.VndDocuments.SingleAsync(x => x.Id == seeded.VndId);
        Assert.Equal(ApprovalProcessStatus.Cancelled, process.Status);
        Assert.NotNull(process.CompletedAt);
        Assert.Equal(RedactionApprovalStatus.Draft, redaction.ApprovalStatus);
        Assert.Equal(VndStatus.Draft, vnd.Status); // редакция №1 → черновик
    }

    [Fact]
    public async Task Cancel_ByNonInitiatorWithoutPrivilege_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var svc = NewService(db); // FakeCurrentUser = Approver1, без прав главного редактора

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CancelAsync(seeded.VndId, 999));
    }

    [Fact]
    public async Task Cancel_AlreadyApproved_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        seeded.Status = ApprovalProcessStatus.Approved;
        await db.SaveChangesAsync();
        var svc = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelAsync(seeded.VndId, Initiator));
    }

    [Fact]
    public async Task Decide_ApproveWithComment_MarksStageForRepeat()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var seeded = SeedTwoStageProcess(db);
        var stageId = seeded.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(seeded.VndId, stageId,
            new ApprovalDecisionRequest { Decision = ApprovalDecisionType.ApproveWithComment, Comment = "Правки" }, Approver1);

        var stage = await db.VndApprovalStages.SingleAsync(s => s.Id == stageId);
        Assert.Equal(ApprovalStageDecision.ApprovedWithComment, stage.PrimaryDecision);
        Assert.True(stage.ParticipatesInRepeat); // с замечанием → участвует в повторном круге
    }
}
