using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;

namespace Delosfera.Tests;

/// <summary>
/// Агрегатор трейсов маршрутов (MINE-1): варианты маршрута, возвраты/доработки и
/// узкие места считаются поверх сидовых данных Workflow.
///
/// Проверяем не форму запросов, а смысл показателей: одинаковые последовательности
/// этапов схлопываются в один вариант с верной частотой; отклонение поднимает долю
/// возвратов и помечает нужный этап; ожидание участника попадает в узкие места того
/// этапа, на котором он стоял.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProcessMiningServiceTests
{
    private readonly PostgresFixture _postgres;

    public ProcessMiningServiceTests(PostgresFixture postgres) => _postgres = postgres;

    // Будни, чтобы астрономические часы не путались с рабочими; сам тест считает в
    // астрономических (BusinessHours=false) для детерминизма.
    private static readonly DateTime Base = new(2025, 6, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Report_AggregatesVariants_Rework_And_Bottlenecks()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var author = new User
        {
            FullName = "Автор",
            Email = $"author-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        var slowApprover = new User
        {
            FullName = "Медленный согласующий",
            Email = $"slow-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.AddRange(author, slowApprover);
        await db.SaveChangesAsync();

        // Вариант «Согласование → Подписание», прошёл дважды, оба раза завершён за 5 ч.
        await SeedRouteAsync(db, author.Id, RouteInstanceStatus.Approved, durationHours: 5,
            steps: new[]
            {
                (StepKind.Approval, false, slowApprover.Id, ResolutionType.Approved, 3.0),
                (StepKind.Signing, false, author.Id, ResolutionType.Approved, 1.0),
            });
        await SeedRouteAsync(db, author.Id, RouteInstanceStatus.Approved, durationHours: 5,
            steps: new[]
            {
                (StepKind.Approval, false, slowApprover.Id, ResolutionType.Approved, 3.0),
                (StepKind.Signing, false, author.Id, ResolutionType.Approved, 1.0),
            });

        // Вариант «Согласование» с возвратом: отклонение на этапе согласования,
        // маршрут ушёл на доработку.
        await SeedRouteAsync(db, author.Id, RouteInstanceStatus.OnRevision, durationHours: null,
            steps: new[]
            {
                (StepKind.Approval, false, slowApprover.Id, ResolutionType.Rejected, 3.0),
            });

        var service = new ProcessMiningService(db);
        var report = await service.GetReportAsync(new ProcessMiningFilter
        {
            DocumentType = (int)DocumentType.Vnd,
            BusinessHours = false,
        });

        // ── Сводка ───────────────────────────────────────────────────────────
        Assert.Equal(3, report.Overview.TotalInstances);
        Assert.Equal(2, report.Overview.CompletedInstances);
        Assert.Equal(1, report.Overview.RunningInstances);
        Assert.Equal(1, report.Overview.ReworkInstances);
        Assert.Equal(33.3, report.Overview.ReworkRatePercent);
        Assert.Equal(2, report.Overview.DistinctVariants);
        Assert.Equal(5, report.Overview.AverageDurationHours);
        Assert.Equal(5, report.Overview.MedianDurationHours);

        // ── Варианты ───────────────────────────────────────────────────────────
        var top = report.Variants[0];
        Assert.Equal("Согласование → Подписание", top.Signature);
        Assert.Equal(2, top.Frequency);
        Assert.Equal(66.7, top.Percent);
        Assert.Equal(5, top.AverageDurationHours);

        var reworkVariant = report.Variants.Single(v => v.Signature == "Согласование");
        Assert.Equal(1, reworkVariant.Frequency);
        Assert.Equal(1, reworkVariant.ReworkCount);

        // ── Возвраты по этапам ───────────────────────────────────────────────
        var approvalRework = report.Rework.Single(r => r.StepLabel == "Согласование");
        Assert.Equal(3, approvalRework.Occurrences); // 3 маршрута прошли согласование
        Assert.Equal(1, approvalRework.Returns);     // одно отклонение
        Assert.Equal(33.3, approvalRework.ReturnRatePercent);

        var signingRework = report.Rework.Single(r => r.StepLabel == "Подписание");
        Assert.Equal(0, signingRework.Returns);

        // ── Узкие места ──────────────────────────────────────────────────────
        // Согласование ждали по 3 ч, подписание — по 1 ч: согласование дольше.
        var slowestStep = report.BottleneckSteps[0];
        Assert.Equal("Согласование", slowestStep.StepLabel);
        Assert.Equal(3, slowestStep.AverageWaitHours);

        // Медленный согласующий стоял на трёх этапах согласования по 3 ч — он же
        // и самый долгий участник.
        var slowest = report.BottleneckParticipants[0];
        Assert.Equal(slowApprover.Id, slowest.UserId);
        Assert.Equal(3, slowest.Samples);
        Assert.Equal(3, slowest.AverageWaitHours);

        Assert.Equal("ч", report.DurationUnit);
    }

    [Fact]
    public async Task Report_FiltersOutOtherDocumentTypes()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var user = new User
        {
            FullName = "Пользователь",
            Email = $"u-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SeedRouteAsync(db, user.Id, RouteInstanceStatus.Approved, durationHours: 4,
            steps: new[] { (StepKind.Approval, false, user.Id, ResolutionType.Approved, 2.0) },
            docType: DocumentType.Vnd);
        await SeedRouteAsync(db, user.Id, RouteInstanceStatus.Approved, durationHours: 4,
            steps: new[] { (StepKind.Approval, false, user.Id, ResolutionType.Approved, 2.0) },
            docType: DocumentType.Sz);

        var service = new ProcessMiningService(db);

        var vnd = await service.GetReportAsync(new ProcessMiningFilter { DocumentType = (int)DocumentType.Vnd });
        Assert.Equal(1, vnd.Overview.TotalInstances);

        var all = await service.GetReportAsync(new ProcessMiningFilter());
        Assert.Equal(2, all.Overview.TotalInstances);

        var options = await service.GetFilterOptionsAsync();
        Assert.Contains(options.DocumentTypes, o => o.Id == (int)DocumentType.Vnd);
        Assert.Contains(options.DocumentTypes, o => o.Id == (int)DocumentType.Sz);
    }

    /// <summary>
    /// Один маршрут: документ, экземпляр маршрута и его этапы с участниками и
    /// резолюциями. Каждый этап активируется в момент старта; решение участника
    /// проставляется через resolveAfterHours часов после активации.
    /// </summary>
    private static async Task SeedRouteAsync(
        DelosferaDbContext db,
        int authorId,
        RouteInstanceStatus status,
        double? durationHours,
        (StepKind Kind, bool IsFinalMethodology, int UserId, ResolutionType ResType, double ResolveAfterHours)[] steps,
        DocumentType docType = DocumentType.Vnd)
    {
        var document = new Document
        {
            Type = docType,
            Title = "Документ для process mining",
            StatusCode = "OnApproval",
            AuthorId = authorId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var routeSteps = new List<RouteStep>();
        var order = 1;
        foreach (var s in steps)
        {
            routeSteps.Add(new RouteStep
            {
                Order = order++,
                Mode = StepMode.Sequential,
                Kind = s.Kind,
                IsFinalMethodology = s.IsFinalMethodology,
                ActivatedAt = Base,
                Participants = new List<RouteParticipant>
                {
                    new()
                    {
                        UserId = s.UserId,
                        State = ParticipantState.Done,
                        ActivatedAt = Base,
                    },
                },
            });
        }

        var instance = new RouteInstance
        {
            DocumentId = document.Id,
            TemplateId = null,
            Status = status,
            CurrentStepOrder = steps.Length,
            StartedAt = Base,
            FinishedAt = durationHours is { } h ? Base.AddHours(h) : null,
            Steps = routeSteps,
        };
        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();

        // Резолюции добавляем после сохранения — у участников появились идентификаторы.
        for (var i = 0; i < steps.Length; i++)
        {
            var participant = routeSteps[i].Participants.First();
            db.Resolutions.Add(new Resolution
            {
                RouteParticipantId = participant.Id,
                Type = steps[i].ResType,
                At = Base.AddHours(steps[i].ResolveAfterHours),
            });
        }
        await db.SaveChangesAsync();
    }
}
