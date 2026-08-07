using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.Common;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Vnd;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Analytics.Services;

public class VndAnalyticsService : IVndAnalyticsService
{
    private readonly DelosferaDbContext _db;

    public VndAnalyticsService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<VndOverviewResponse> GetOverviewAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthAgo = DateTime.UtcNow.AddDays(-30);

        var statusCounts = await _db.VndDocuments
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountOf(VndStatus s) => statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var total = statusCounts.Sum(x => x.Count);

        var dueDates = await _db.VndDocuments
            .Where(v => v.DueActualizationDate != null && v.Status == VndStatus.Active)
            .Select(v => v.DueActualizationDate!.Value)
            .ToListAsync();

        var overdue = dueDates.Count(d => ActualizationThresholds.Resolve(d, today) == ActualizationBucket.Overdue);
        var critical = dueDates.Count(d => ActualizationThresholds.Resolve(d, today) == ActualizationBucket.Critical);

        var approvalsInProgress = await _db.VndApprovalProcesses.CountAsync(p =>
            p.Status == ApprovalProcessStatus.Primary
            || p.Status == ApprovalProcessStatus.Repeated
            || p.Status == ApprovalProcessStatus.RevisionNeeded
            || p.Status == ApprovalProcessStatus.FinalHold);

        var createdLast30 = await _db.VndDocuments.CountAsync(v => v.CreatedAt >= monthAgo);

        var publishedLast30 = await _db.VndApprovalProcesses.CountAsync(p =>
            p.Status == ApprovalProcessStatus.Approved && p.CompletedAt != null && p.CompletedAt >= monthAgo);

        var completedProcesses = await _db.VndApprovalProcesses
            .Where(p => p.CompletedAt != null)
            .Select(p => new { p.PrimaryStartedAt, CompletedAt = p.CompletedAt!.Value })
            .ToListAsync();

        var avgDuration = completedProcesses.Count > 0
            ? Math.Round(completedProcesses.Average(p => (p.CompletedAt - p.PrimaryStartedAt).TotalDays), 1)
            : 0;

        var stageDecisions = await _db.VndApprovalStages
            .Select(s => new { s.PrimaryDecision, s.RepeatDecision, s.FinalHoldDecision })
            .ToListAsync();

        var madeDecisions = stageDecisions
            .SelectMany(s => new[] { s.PrimaryDecision, s.RepeatDecision, s.FinalHoldDecision }
                .Where(d => d.HasValue && d.Value != ApprovalStageDecision.Pending)
                .Select(d => d!.Value))
            .ToList();

        var timeoutRate = madeDecisions.Count > 0
            ? Math.Round(madeDecisions.Count(d => d == ApprovalStageDecision.AutoApprovedByTimeout) * 100.0 / madeDecisions.Count, 1)
            : 0;

        return new VndOverviewResponse
        {
            Total = total,
            Active = CountOf(VndStatus.Active),
            OnActualization = CountOf(VndStatus.OnActualization),
            OnReview = CountOf(VndStatus.Review),
            OnConsolidation = CountOf(VndStatus.Consolidation),
            Archived = CountOf(VndStatus.Archived),
            Draft = CountOf(VndStatus.Draft),
            RequiresAttention = critical + overdue,
            Overdue = overdue,
            ApprovalsInProgress = approvalsInProgress,
            CreatedLast30Days = createdLast30,
            PublishedLast30Days = publishedLast30,
            AverageApprovalDurationDays = avgDuration,
            TimeoutDecisionRatePercent = timeoutRate
        };
    }

    public async Task<List<ChartCategoryPoint>> GetStatusDistributionAsync(string language)
    {
        var counts = await _db.VndDocuments
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var total = counts.Sum(c => c.Count);

        return counts
            .Select(c => new ChartCategoryPoint
            {
                Id = (int)c.Status,
                Label = StatusLabel(c.Status, language),
                Value = c.Count,
                Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
            })
            .OrderByDescending(c => c.Value)
            .ToList();
    }

    public async Task<List<ChartCategoryPoint>> GetTypeDistributionAsync(string language)
    {
        var counts = await _db.VndDocuments
            .GroupBy(v => v.TypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToListAsync();

        var ids = counts.Select(c => c.TypeId).ToList();
        var types = await _db.TypesVnd.Where(t => ids.Contains(t.Id)).ToListAsync();
        var total = counts.Sum(c => c.Count);

        return counts
            .Select(c =>
            {
                var type = types.FirstOrDefault(t => t.Id == c.TypeId);
                return new ChartCategoryPoint
                {
                    Id = c.TypeId,
                    Label = type != null ? type.ResolveTitle(language) : $"#{c.TypeId}",
                    Value = c.Count,
                    Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
                };
            })
            .OrderByDescending(c => c.Value)
            .ToList();
    }

    public async Task<List<ChartCategoryPoint>> GetDeveloperDistributionAsync(string language, int top = 10)
    {
        var counts = await _db.VndDocuments
            .GroupBy(v => v.DeveloperId)
            .Select(g => new { DeveloperId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var total = counts.Sum(c => c.Count);
        var topCounts = counts.Take(top).ToList();
        var otherCount = counts.Skip(top).Sum(c => c.Count);

        var ids = topCounts.Select(c => c.DeveloperId).ToList();
        var orgUnits = await _db.OrganizationUnits.Where(o => ids.Contains(o.Id)).ToListAsync();

        var result = topCounts
            .Select(c =>
            {
                var org = orgUnits.FirstOrDefault(o => o.Id == c.DeveloperId);
                return new ChartCategoryPoint
                {
                    Id = c.DeveloperId,
                    Label = org != null ? org.ResolveTitle(language) : $"#{c.DeveloperId}",
                    Value = c.Count,
                    Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
                };
            })
            .ToList();

        if (otherCount > 0)
        {
            result.Add(new ChartCategoryPoint
            {
                Id = null,
                Label = language == "en" ? "Others" : language == "kg" ? "Башкалар" : "Остальные",
                Value = otherCount,
                Percent = total > 0 ? Math.Round(otherCount * 100.0 / total, 1) : 0
            });
        }

        return result;
    }

    public async Task<List<ChartCategoryPoint>> GetSecurityLevelDistributionAsync(string language)
    {
        var counts = await _db.VndDocuments
            .GroupBy(v => v.SecrecyLevelId)
            .Select(g => new { SecrecyLevelId = g.Key, Count = g.Count() })
            .ToListAsync();

        var ids = counts.Select(c => c.SecrecyLevelId).ToList();
        var levels = await _db.SecurityLevels.Where(s => ids.Contains(s.Id)).ToListAsync();
        var total = counts.Sum(c => c.Count);

        return counts
            .Select(c =>
            {
                var level = levels.FirstOrDefault(s => s.Id == c.SecrecyLevelId);
                return new ChartCategoryPoint
                {
                    Id = c.SecrecyLevelId,
                    Label = level != null ? level.ResolveTitle(language) : $"#{c.SecrecyLevelId}",
                    Value = c.Count,
                    Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
                };
            })
            .OrderByDescending(c => c.Value)
            .ToList();
    }

    public async Task<List<ChartCategoryPoint>> GetRubricDistributionAsync(string language, int top = 10)
    {
        var counts = await _db.VndDocuments
            .SelectMany(v => v.Rubrics.Select(r => r.Id))
            .GroupBy(id => id)
            .Select(g => new { RubricId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        var ids = counts.Select(c => c.RubricId).ToList();
        var rubrics = await _db.Rubrics.Where(r => ids.Contains(r.Id)).ToListAsync();
        var total = counts.Sum(c => c.Count);

        return counts
            .Select(c =>
            {
                var rubric = rubrics.FirstOrDefault(r => r.Id == c.RubricId);
                return new ChartCategoryPoint
                {
                    Id = c.RubricId,
                    Label = rubric != null ? rubric.ResolveTitle(language) : $"#{c.RubricId}",
                    Value = c.Count,
                    Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
                };
            })
            .OrderByDescending(c => c.Value)
            .ToList();
    }

    public async Task<List<ChartCategoryPoint>> GetKeywordCloudAsync(string language, int top = 30)
    {
        var counts = await _db.VndDocuments
            .SelectMany(v => v.Keywords.Select(k => k.Id))
            .GroupBy(id => id)
            .Select(g => new { KeywordId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync();

        var ids = counts.Select(c => c.KeywordId).ToList();
        var keywords = await _db.Keywords.Where(k => ids.Contains(k.Id)).ToListAsync();
        var total = counts.Sum(c => c.Count);

        return counts
            .Select(c =>
            {
                var keyword = keywords.FirstOrDefault(k => k.Id == c.KeywordId);
                return new ChartCategoryPoint
                {
                    Id = c.KeywordId,
                    Label = keyword != null ? keyword.ResolveTitle(language) : $"#{c.KeywordId}",
                    Value = c.Count,
                    Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
                };
            })
            .OrderByDescending(c => c.Value)
            .ToList();
    }

    public async Task<List<VndDynamicsPoint>> GetDynamicsAsync(AnalyticsPeriodRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.DateFrom ?? AnalyticsPeriodBucketing.DefaultFrom(today, request.Granularity);
        var to = request.DateTo ?? today;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var created = await _db.VndDocuments
            .Where(v => v.CreatedAt >= fromDt && v.CreatedAt <= toDt)
            .Select(v => v.CreatedAt)
            .ToListAsync();

        var sentToApproval = await _db.VndApprovalProcesses
            .Where(p => p.PrimaryStartedAt >= fromDt && p.PrimaryStartedAt <= toDt)
            .Select(p => p.PrimaryStartedAt)
            .ToListAsync();

        var published = await _db.VndApprovalProcesses
            .Where(p => p.CompletedAt != null && p.CompletedAt >= fromDt && p.CompletedAt <= toDt)
            .Select(p => p.CompletedAt!.Value)
            .ToListAsync();

        var archived = await _db.VndDocuments
            .Where(v => v.ArchivedDate != null && v.ArchivedDate >= from && v.ArchivedDate <= to)
            .Select(v => v.ArchivedDate!.Value)
            .ToListAsync();

        var buckets = AnalyticsPeriodBucketing.GeneratePeriods(from, to, request.Granularity);

        return buckets.Select(b => new VndDynamicsPoint
        {
            PeriodStart = b.Start,
            PeriodLabel = b.Label,
            Created = created.Count(d =>
                AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(d), request.Granularity) == b.Start),
            SentToApproval = sentToApproval.Count(d =>
                AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(d), request.Granularity) == b.Start),
            Published = published.Count(d =>
                AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(d), request.Granularity) == b.Start),
            Archived = archived.Count(d =>
                AnalyticsPeriodBucketing.BucketStart(d, request.Granularity) == b.Start)
        }).ToList();
    }

    public async Task<List<VndActualizationTrendPoint>> GetActualizationTrendAsync(AnalyticsPeriodRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.DateFrom ?? AnalyticsPeriodBucketing.DefaultFrom(today, request.Granularity);
        var to = request.DateTo ?? today;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var records = await _db.Set<VndActualizationRecord>()
            .Where(r => r.StartedAt >= fromDt && r.StartedAt <= toDt
                || (r.PublishedAt != null && r.PublishedAt >= fromDt && r.PublishedAt <= toDt))
            .Select(r => new { r.StartedAt, r.PublishedAt, r.HadChanges })
            .ToListAsync();

        var buckets = AnalyticsPeriodBucketing.GeneratePeriods(from, to, request.Granularity);

        return buckets.Select(b =>
        {
            var started = records.Count(r =>
                AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(r.StartedAt), request.Granularity) == b.Start);

            var publishedInBucket = records
                .Where(r => r.PublishedAt != null &&
                    AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(r.PublishedAt.Value), request.Granularity) == b.Start)
                .ToList();

            var avgDuration = publishedInBucket.Count > 0
                ? Math.Round(publishedInBucket.Average(r => (r.PublishedAt!.Value - r.StartedAt).TotalDays), 1)
                : 0;

            return new VndActualizationTrendPoint
            {
                PeriodStart = b.Start,
                PeriodLabel = b.Label,
                Started = started,
                Published = publishedInBucket.Count,
                PublishedWithChanges = publishedInBucket.Count(r => r.HadChanges == true),
                AverageDurationDays = avgDuration
            };
        }).ToList();
    }

    public async Task<VndApprovalPerformanceResponse> GetApprovalPerformanceAsync(AnalyticsPeriodRequest? request)
    {
        var granularity = request?.Granularity ?? AnalyticsGranularity.Month;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request?.DateFrom ?? AnalyticsPeriodBucketing.DefaultFrom(today, granularity);
        var to = request?.DateTo ?? today;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var processes = await _db.VndApprovalProcesses
            .Where(p => p.PrimaryStartedAt >= fromDt && p.PrimaryStartedAt <= toDt)
            .Select(p => new { p.Status, p.PrimaryStartedAt, p.CompletedAt })
            .ToListAsync();

        var approved = processes.Count(p => p.Status == ApprovalProcessStatus.Approved);
        var rejected = processes.Count(p => p.Status == ApprovalProcessStatus.Rejected);
        var cancelled = processes.Count(p => p.Status == ApprovalProcessStatus.Cancelled);
        var inProgress = processes.Count(p =>
            p.Status is ApprovalProcessStatus.Primary or ApprovalProcessStatus.Repeated
                or ApprovalProcessStatus.RevisionNeeded or ApprovalProcessStatus.FinalHold);

        var finished = approved + rejected + cancelled;
        var approvalRate = finished > 0 ? Math.Round(approved * 100.0 / finished, 1) : 0;

        // Доля процессов, где потребовалось повторное согласование (были в статусе доработки/повтора)
        var revisionRequiredIds = await _db.VndApprovalStages
            .Where(s => s.ParticipatesInRepeat)
            .Select(s => s.ApprovalProcessId)
            .Distinct()
            .ToListAsync();

        var revisionRate = processes.Count > 0
            ? Math.Round(revisionRequiredIds.Count * 100.0 / processes.Count, 1)
            : 0;

        var durations = processes
            .Where(p => p.CompletedAt != null)
            .Select(p => (p.CompletedAt!.Value - p.PrimaryStartedAt).TotalDays)
            .OrderBy(d => d)
            .ToList();

        var avgDuration = durations.Count > 0 ? Math.Round(durations.Average(), 1) : 0;
        var medianDuration = durations.Count > 0 ? Math.Round(Median(durations), 1) : 0;

        var buckets = AnalyticsPeriodBucketing.GeneratePeriods(from, to, granularity);
        var completedWithDates = processes
            .Where(p => p.CompletedAt != null)
            .Select(p => new { p.CompletedAt, Duration = (p.CompletedAt!.Value - p.PrimaryStartedAt).TotalDays })
            .ToList();

        var trend = buckets.Select(b =>
        {
            var inBucket = completedWithDates
                .Where(p => AnalyticsPeriodBucketing.BucketStart(DateOnly.FromDateTime(p.CompletedAt!.Value), granularity) == b.Start)
                .ToList();

            return new ChartTimePoint
            {
                PeriodStart = b.Start,
                PeriodLabel = b.Label,
                Value = inBucket.Count > 0 ? (int)Math.Round(inBucket.Average(p => p.Duration)) : 0
            };
        }).ToList();

        return new VndApprovalPerformanceResponse
        {
            TotalProcesses = processes.Count,
            Approved = approved,
            Rejected = rejected,
            Cancelled = cancelled,
            InProgress = inProgress,
            ApprovalRatePercent = approvalRate,
            RevisionRatePercent = revisionRate,
            AverageDurationDays = avgDuration,
            MedianDurationDays = medianDuration,
            DurationTrend = trend
        };
    }

    public async Task<List<VndApproverWorkloadItem>> GetApproverWorkloadAsync(bool byUser = false)
    {
        var stages = await _db.VndApprovalStages
            .Select(s => new
            {
                s.OrgUnitId,
                s.ApproverUserId,
                s.PrimaryDecision,
                s.PrimaryDecidedAt,
                s.ApprovalProcess!.PrimaryStartedAt,
                s.RepeatDecision,
                s.RepeatDecidedAt,
                s.ApprovalProcess!.RepeatStartedAt,
                s.FinalHoldDecision,
                s.FinalHoldDecidedAt,
                s.ApprovalProcess!.FinalHoldStartedAt,
                s.ParticipatesInRepeat
            })
            .ToListAsync();

        var orgUnits = await _db.OrganizationUnits.ToDictionaryAsync(o => o.Id, o => o.TitleRu);

        var groups = byUser
            ? stages.GroupBy(s => (s.OrgUnitId, ApproverUserId: (int?)s.ApproverUserId))
            : stages.GroupBy(s => (s.OrgUnitId, ApproverUserId: (int?)null));

        Dictionary<int, string>? userNames = null;
        if (byUser)
        {
            userNames = await _db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        }

        var result = new List<VndApproverWorkloadItem>();

        foreach (var group in groups)
        {
            var decided = new List<(ApprovalStageDecision Decision, double Hours)>();
            int pending = 0;

            foreach (var s in group)
            {
                if (s.PrimaryDecision != ApprovalStageDecision.Pending)
                    decided.Add((s.PrimaryDecision, (s.PrimaryDecidedAt!.Value - s.PrimaryStartedAt).TotalHours));
                else
                    pending++;

                if (s.RepeatDecision.HasValue && s.RepeatDecision != ApprovalStageDecision.Pending
                    && s.RepeatStartedAt.HasValue)
                    decided.Add((s.RepeatDecision.Value, (s.RepeatDecidedAt!.Value - s.RepeatStartedAt.Value).TotalHours));
                else if (s.ParticipatesInRepeat && (s.RepeatDecision is null or ApprovalStageDecision.Pending))
                    pending++;

                if (s.FinalHoldDecision.HasValue && s.FinalHoldDecision != ApprovalStageDecision.Pending
                    && s.FinalHoldStartedAt.HasValue)
                    decided.Add((s.FinalHoldDecision.Value, (s.FinalHoldDecidedAt!.Value - s.FinalHoldStartedAt.Value).TotalHours));
                else if (s.FinalHoldStartedAt.HasValue && (s.FinalHoldDecision is null or ApprovalStageDecision.Pending))
                    pending++;
            }

            var onTime = decided.Where(d => d.Decision != ApprovalStageDecision.AutoApprovedByTimeout).ToList();
            var timeout = decided.Count(d => d.Decision == ApprovalStageDecision.AutoApprovedByTimeout);
            var withComments = decided.Count(d =>
                d.Decision == ApprovalStageDecision.ApprovedWithComment || d.Decision == ApprovalStageDecision.Rejected);

            result.Add(new VndApproverWorkloadItem
            {
                OrgUnitId = group.Key.OrgUnitId,
                OrgUnitLabel = orgUnits.GetValueOrDefault(group.Key.OrgUnitId, $"#{group.Key.OrgUnitId}"),
                ApproverUserId = group.Key.ApproverUserId,
                ApproverLabel = group.Key.ApproverUserId.HasValue
                    ? userNames?.GetValueOrDefault(group.Key.ApproverUserId.Value, $"#{group.Key.ApproverUserId}")
                    : null,
                TotalStages = group.Count(),
                DecidedOnTime = onTime.Count,
                AutoApprovedByTimeout = timeout,
                WithCommentsOrRejected = withComments,
                Pending = pending,
                AverageDecisionHours = onTime.Count > 0 ? Math.Round(onTime.Average(d => d.Hours), 1) : 0,
                TimeoutRatePercent = decided.Count > 0 ? Math.Round(timeout * 100.0 / decided.Count, 1) : 0
            });
        }

        return result.OrderByDescending(r => r.TimeoutRatePercent).ThenByDescending(r => r.TotalStages).ToList();
    }

    public async Task<List<VndOrgUnitStatusMatrixItem>> GetOrgUnitStatusMatrixAsync(string language)
    {
        var counts = await _db.VndDocuments
            .GroupBy(v => new { v.DeveloperId, v.Status })
            .Select(g => new { g.Key.DeveloperId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var ids = counts.Select(c => c.DeveloperId).Distinct().ToList();
        var orgUnits = await _db.OrganizationUnits.Where(o => ids.Contains(o.Id)).ToListAsync();

        return counts.Select(c =>
        {
            var org = orgUnits.FirstOrDefault(o => o.Id == c.DeveloperId);
            return new VndOrgUnitStatusMatrixItem
            {
                OrgUnitId = c.DeveloperId,
                OrgUnitLabel = org != null ? org.ResolveTitle(language) : $"#{c.DeveloperId}",
                Status = MapStatusToCode(c.Status),
                Count = c.Count
            };
        }).ToList();
    }

    public async Task<byte[]> ExportOverviewCsvAsync(string language)
    {
        var overview = await GetOverviewAsync();
        var statusDistribution = await GetStatusDistributionAsync(language);
        var typeDistribution = await GetTypeDistributionAsync(language);
        var developerDistribution = await GetDeveloperDistributionAsync(language, top: 15);
        var securityLevelDistribution = await GetSecurityLevelDistributionAsync(language);
        var rubricDistribution = await GetRubricDistributionAsync(language, top: 15);

        var sb = new System.Text.StringBuilder();
        const string sep = ";";

        void WriteRow(params string[] cells) => sb.AppendLine(string.Join(sep, cells.Select(EscapeCsv)));
        void WriteSection(string title, IEnumerable<ChartCategoryPoint> points)
        {
            sb.AppendLine();
            WriteRow(title);
            WriteRow("Показатель", "Количество", "Доля, %");
            foreach (var p in points)
                WriteRow(p.Label, p.Value.ToString(), p.Percent.ToString("0.0"));
        }

        WriteRow($"Отчёт по ВНД — сформирован {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC");

        sb.AppendLine();
        WriteRow("KPI-показатели");
        WriteRow("Показатель", "Значение");
        WriteRow("Всего документов", overview.Total.ToString());
        WriteRow("Действующие", overview.Active.ToString());
        WriteRow("На актуализации", overview.OnActualization.ToString());
        WriteRow("На согласовании", overview.OnReview.ToString());
        WriteRow("На консолидации", overview.OnConsolidation.ToString());
        WriteRow("Архивированные", overview.Archived.ToString());
        WriteRow("Черновики", overview.Draft.ToString());
        WriteRow("Требуют внимания (критично + просрочено)", overview.RequiresAttention.ToString());
        WriteRow("Просрочено по актуализации", overview.Overdue.ToString());
        WriteRow("Активных согласований сейчас", overview.ApprovalsInProgress.ToString());
        WriteRow("Создано за последние 30 дней", overview.CreatedLast30Days.ToString());
        WriteRow("Опубликовано за последние 30 дней", overview.PublishedLast30Days.ToString());
        WriteRow("Средняя длительность согласования, дн.", overview.AverageApprovalDurationDays.ToString("0.0"));
        WriteRow("Доля решений по таймауту, %", overview.TimeoutDecisionRatePercent.ToString("0.0"));

        WriteSection("Распределение по статусам", statusDistribution);
        WriteSection("Распределение по видам документа", typeDistribution);
        WriteSection("Топ подразделений-разработчиков", developerDistribution);
        WriteSection("Распределение по уровням секретности", securityLevelDistribution);
        WriteSection("Топ рубрик классификатора", rubricDistribution);

        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        var body = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // --- Вспомогательные методы ---

    private static string StatusLabel(VndStatus status, string language) => (status, language) switch
    {
        (VndStatus.Active, "en") => "Active",
        (VndStatus.OnActualization, "en") => "Under actualization",
        (VndStatus.Review, "en") => "Under approval",
        (VndStatus.Consolidation, "en") => "Under consolidation",
        (VndStatus.Archived, "en") => "Archived",
        (VndStatus.Draft, "en") => "Draft",
        (VndStatus.Active, _) => "Действующий",
        (VndStatus.OnActualization, _) => "На актуализации",
        (VndStatus.Review, _) => "На согласовании",
        (VndStatus.Consolidation, _) => "На консолидации",
        (VndStatus.Archived, _) => "Архивирован",
        (VndStatus.Draft, _) => "Черновик",
        _ => status.ToString()
    };

    private static string MapStatusToCode(VndStatus status) => status switch
    {
        VndStatus.Active => "active",
        VndStatus.OnActualization => "onact",
        VndStatus.Review => "review",
        VndStatus.Consolidation => "consol",
        VndStatus.Archived => "arch",
        VndStatus.Draft => "draft",
        _ => "onact"
    };

    private static double Median(List<double> sorted)
    {
        if (sorted.Count == 0) return 0;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }
}