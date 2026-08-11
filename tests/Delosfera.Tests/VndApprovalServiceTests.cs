using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

public class VndApprovalServiceTests
{
    private const int Approver1 = 200;
    private const int Approver2 = 201;

    private static VndApprovalService NewService(DelosferaDbContext db) =>
        new(db, new NoopFileStorage(), new NoopNotificationService(),
            new FakeCurrentUser(Approver1), NullLogger<VndApprovalService>.Instance);

    // Двухэтапный процесс на первичной фазе: решение по одному этапу не завершает фазу,
    // поэтому изолируем логику DecideAsync без тяжёлого перехода фаз.
    private static VndApprovalProcess SeedTwoStageProcess(DelosferaDbContext db)
    {
        // LoadProcessForVndAsync ищет редакцию по VndId, затем процесс по RedactionId и делает
        // Include(Vnd). Связь process→Vnd обязательная, поэтому Include превращается во внутренний
        // JOIN — без строки VndDocument процесс отфильтровывается. Сидим документ, редакцию и процесс
        // с явными ключами.
        db.VndDocuments.Add(new VndDocument { Id = 1, Code = "TEST-10001", TitleRu = "Тестовый ВНД" });
        db.VndRedactions.Add(new VndRedaction { Id = 1, VndId = 1, Number = 1, Code = "TEST-Р1", DocFileRuId = 1 });

        var process = new VndApprovalProcess
        {
            Id = 1,
            VndId = 1,
            RedactionId = 1,
            InitiatorUserId = 100,
            Status = ApprovalProcessStatus.Primary,
            PrimaryDeadlineMinutes = 60,
            RepeatDeadlineMinutes = 60,
            FinalHoldDeadlineMinutes = 60,
            PrimaryStartedAt = DateTime.UtcNow,
            Stages =
            [
                new VndApprovalStage { Order = 1, OrgUnitId = 1, ApproverUserId = Approver1, PrimaryDecision = ApprovalStageDecision.Pending },
                new VndApprovalStage { Order = 2, OrgUnitId = 2, ApproverUserId = Approver2, PrimaryDecision = ApprovalStageDecision.Pending },
            ]
        };
        db.VndApprovalProcesses.Add(process);
        db.SaveChanges();
        return process;
    }

    [Fact]
    public async Task Decide_Approve_RecordsDecisionOnStage()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        var stageId = process.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(1, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1);

        var stage = await db.VndApprovalStages.SingleAsync(s => s.Id == stageId);
        Assert.Equal(ApprovalStageDecision.Approved, stage.PrimaryDecision);
        Assert.NotNull(stage.PrimaryDecidedAt);
    }

    [Fact]
    public async Task Decide_ByUserNotAssignedToStage_Throws()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        var stageId = process.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DecideAsync(1, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, 999));
    }

    [Fact]
    public async Task Decide_RejectWithoutComment_Throws()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        var stageId = process.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DecideAsync(1, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Reject }, Approver1));
    }

    [Fact]
    public async Task Decide_Twice_Throws()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        var stageId = process.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(1, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DecideAsync(1, stageId, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1));
    }

    [Fact]
    public async Task Decide_UnknownStage_Throws()
    {
        using var db = TestSupport.NewDb();
        SeedTwoStageProcess(db);
        var svc = NewService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.DecideAsync(1, 999999, new ApprovalDecisionRequest { Decision = ApprovalDecisionType.Approve }, Approver1));
    }

    [Fact]
    public async Task Cancel_ByInitiator_SetsCancelledAndRevertsToDraft()
    {
        using var db = TestSupport.NewDb();
        SeedTwoStageProcess(db);
        var svc = NewService(db);

        await svc.CancelAsync(1, 100); // 100 = InitiatorUserId

        var process = await db.VndApprovalProcesses.SingleAsync();
        var redaction = await db.VndRedactions.SingleAsync();
        var vnd = await db.VndDocuments.SingleAsync();
        Assert.Equal(ApprovalProcessStatus.Cancelled, process.Status);
        Assert.NotNull(process.CompletedAt);
        Assert.Equal(RedactionApprovalStatus.Draft, redaction.ApprovalStatus);
        Assert.Equal(VndStatus.Draft, vnd.Status); // редакция №1 → черновик
    }

    [Fact]
    public async Task Cancel_ByNonInitiatorWithoutPrivilege_Throws()
    {
        using var db = TestSupport.NewDb();
        SeedTwoStageProcess(db);
        var svc = NewService(db); // FakeCurrentUser = Approver1, без прав главного редактора

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CancelAsync(1, 999));
    }

    [Fact]
    public async Task Cancel_AlreadyApproved_Throws()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        process.Status = ApprovalProcessStatus.Approved;
        await db.SaveChangesAsync();
        var svc = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelAsync(1, 100));
    }

    [Fact]
    public async Task Decide_ApproveWithComment_MarksStageForRepeat()
    {
        using var db = TestSupport.NewDb();
        var process = SeedTwoStageProcess(db);
        var stageId = process.Stages.First(s => s.ApproverUserId == Approver1).Id;
        var svc = NewService(db);

        await svc.DecideAsync(1, stageId,
            new ApprovalDecisionRequest { Decision = ApprovalDecisionType.ApproveWithComment, Comment = "Правки" }, Approver1);

        var stage = await db.VndApprovalStages.SingleAsync(s => s.Id == stageId);
        Assert.Equal(ApprovalStageDecision.ApprovedWithComment, stage.PrimaryDecision);
        Assert.True(stage.ParticipatesInRepeat); // с замечанием → участвует в повторном круге
    }
}
