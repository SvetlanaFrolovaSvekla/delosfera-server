using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response.ProcessMining;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Analytics.Services;

/// <summary>
/// Process mining поверх трейсов маршрутов согласования (MINE-1): варианты маршрута,
/// возвраты/доработки и узкие места. Чистые запросы поверх существующих таблиц Workflow —
/// новых сущностей не заводит.
/// </summary>
public interface IProcessMiningService
{
    Task<ProcessMiningReport> GetReportAsync(ProcessMiningFilter filter);
    Task<ProcessMiningFilterOptions> GetFilterOptionsAsync();
}

public class ProcessMiningService : IProcessMiningService
{
    private readonly DelosferaDbContext _db;

    public ProcessMiningService(DelosferaDbContext db) => _db = db;

    public async Task<ProcessMiningReport> GetReportAsync(ProcessMiningFilter filter)
    {
        // Маршрут — единица наблюдения. Берём только запущенные (есть дата старта):
        // черновики маршрутов трейса ещё не дают.
        var instancesQuery =
            from i in _db.RouteInstances.AsNoTracking()
            join d in _db.Documents.AsNoTracking() on i.DocumentId equals d.Id
            where i.StartedAt != null
            select new InstanceRow
            {
                Id = i.Id,
                TemplateId = i.TemplateId,
                Status = i.Status,
                StartedAt = i.StartedAt,
                FinishedAt = i.FinishedAt,
                DocType = d.Type
            };

        if (filter.DocumentType is { } dt)
            instancesQuery = instancesQuery.Where(x => (int)x.DocType == dt);
        if (filter.TemplateId is { } tid)
            instancesQuery = instancesQuery.Where(x => x.TemplateId == tid);
        if (filter.DateFrom is { } df)
        {
            var fromDt = df.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            instancesQuery = instancesQuery.Where(x => x.StartedAt >= fromDt);
        }
        if (filter.DateTo is { } dto)
        {
            var toDt = dto.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            instancesQuery = instancesQuery.Where(x => x.StartedAt <= toDt);
        }

        var instances = await instancesQuery.ToListAsync();
        var unit = filter.BusinessHours ? "раб.ч" : "ч";

        if (instances.Count == 0)
            return Empty(unit);

        var instanceIds = instances.Select(x => x.Id).ToList();

        // Этапы всех маршрутов выборки.
        var steps = await _db.RouteSteps.AsNoTracking()
            .Where(s => instanceIds.Contains(s.RouteInstanceId))
            .Select(s => new StepRow
            {
                Id = s.Id,
                RouteInstanceId = s.RouteInstanceId,
                Order = s.Order,
                Kind = s.Kind,
                IsFinalMethodology = s.IsFinalMethodology,
                ActivatedAt = s.ActivatedAt
            })
            .ToListAsync();

        var stepById = steps.ToDictionary(s => s.Id);

        // Участники с их резолюциями (одна на участника). Из них берём и решение,
        // и момент решения — для расчёта ожиданий на этапе.
        var participants = await _db.RouteParticipants.AsNoTracking()
            .Where(p => steps.Select(s => s.Id).Contains(p.RouteStepId))
            .Select(p => new ParticipantRow
            {
                RouteStepId = p.RouteStepId,
                UserId = p.UserId,
                State = p.State,
                ActivatedAt = p.ActivatedAt,
                ResType = p.Resolution != null ? (ResolutionType?)p.Resolution.Type : null,
                ResAt = p.Resolution != null ? (DateTime?)p.Resolution.At : null
            })
            .ToListAsync();

        double Elapsed(DateTime from, DateTime to) => filter.BusinessHours
            ? WorkingCalendar.WorkingHoursBetween(from, to)
            : Math.Max((to - from).TotalHours, 0);

        // Возвраты на уровне маршрута: отклонение/вето кого-то из участников либо
        // маршрут ушёл на доработку / отклонён / на арбитраж.
        var reworkInstanceIds = new HashSet<int>(
            instances
                .Where(i => i.Status is RouteInstanceStatus.OnRevision
                    or RouteInstanceStatus.Rejected or RouteInstanceStatus.Arbitration)
                .Select(i => i.Id));
        foreach (var p in participants)
        {
            if (p.ResType is ResolutionType.Rejected or ResolutionType.Veto
                && stepById.TryGetValue(p.RouteStepId, out var st))
                reworkInstanceIds.Add(st.RouteInstanceId);
        }

        // ── Варианты маршрута ────────────────────────────────────────────────
        var variants = BuildVariants(instances, steps, reworkInstanceIds, Elapsed);

        // ── Возвраты/доработки по этапам ─────────────────────────────────────
        var rework = BuildRework(steps, participants, stepById);

        // ── Узкие места ──────────────────────────────────────────────────────
        var (bottleneckSteps, bottleneckParticipants) =
            await BuildBottlenecksAsync(steps, participants, stepById, Elapsed);

        // ── Сводка ───────────────────────────────────────────────────────────
        var completedDurations = instances
            .Where(i => i.FinishedAt != null)
            .Select(i => Elapsed(i.StartedAt!.Value, i.FinishedAt!.Value))
            .OrderBy(x => x)
            .ToList();

        var overview = new ProcessMiningOverview
        {
            TotalInstances = instances.Count,
            CompletedInstances = instances.Count(i => i.FinishedAt != null),
            RunningInstances = instances.Count(i => i.FinishedAt == null),
            ReworkInstances = reworkInstanceIds.Count,
            ReworkRatePercent = Pct(reworkInstanceIds.Count, instances.Count),
            DistinctVariants = variants.Count,
            AverageDurationHours = completedDurations.Count > 0
                ? Math.Round(completedDurations.Average(), 1) : 0,
            MedianDurationHours = Math.Round(Median(completedDurations), 1)
        };

        return new ProcessMiningReport
        {
            Overview = overview,
            Variants = variants,
            Rework = rework,
            BottleneckSteps = bottleneckSteps,
            BottleneckParticipants = bottleneckParticipants,
            DurationUnit = unit
        };
    }

    private static List<ProcessPathVariant> BuildVariants(
        List<InstanceRow> instances,
        List<StepRow> steps,
        HashSet<int> reworkInstanceIds,
        Func<DateTime, DateTime, double> elapsed)
    {
        // Пройденные этапы = те, что реально активировались, по порядку.
        var stepsByInstance = steps
            .Where(s => s.ActivatedAt != null)
            .GroupBy(s => s.RouteInstanceId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.Order)
                      .Select(s => StepLabel(s.Kind, s.IsFinalMethodology))
                      .ToList());

        var bySignature = new Dictionary<string, VariantAccumulator>();
        foreach (var inst in instances)
        {
            var labels = stepsByInstance.TryGetValue(inst.Id, out var l) ? l : new List<string>();
            var signature = labels.Count > 0 ? string.Join(" → ", labels) : "(без пройденных этапов)";

            if (!bySignature.TryGetValue(signature, out var acc))
            {
                acc = new VariantAccumulator { Steps = labels };
                bySignature[signature] = acc;
            }
            acc.Frequency++;
            if (reworkInstanceIds.Contains(inst.Id)) acc.ReworkCount++;
            if (inst.FinishedAt != null)
                acc.Durations.Add(elapsed(inst.StartedAt!.Value, inst.FinishedAt!.Value));
        }

        var total = instances.Count;
        return bySignature
            .Select(kv => new ProcessPathVariant
            {
                Signature = kv.Key,
                Steps = kv.Value.Steps,
                Frequency = kv.Value.Frequency,
                Percent = Pct(kv.Value.Frequency, total),
                AverageDurationHours = kv.Value.Durations.Count > 0
                    ? Math.Round(kv.Value.Durations.Average(), 1) : 0,
                ReworkCount = kv.Value.ReworkCount
            })
            .OrderByDescending(v => v.Frequency)
            .ToList();
    }

    private static List<ProcessReworkStep> BuildRework(
        List<StepRow> steps,
        List<ParticipantRow> participants,
        Dictionary<int, StepRow> stepById)
    {
        // Прохождения этапа считаем по реально активированным этапам.
        var occurrences = steps
            .Where(s => s.ActivatedAt != null)
            .GroupBy(s => StepLabel(s.Kind, s.IsFinalMethodology))
            .ToDictionary(g => g.Key, g => (Kind: (int)g.First().Kind, Count: g.Count()));

        var returns = new Dictionary<string, int>();
        foreach (var p in participants)
        {
            if (p.ResType is not (ResolutionType.Rejected or ResolutionType.Veto)) continue;
            if (!stepById.TryGetValue(p.RouteStepId, out var st)) continue;
            var label = StepLabel(st.Kind, st.IsFinalMethodology);
            returns[label] = returns.GetValueOrDefault(label) + 1;
        }

        return occurrences
            .Select(kv => new ProcessReworkStep
            {
                StepLabel = kv.Key,
                StepKind = kv.Value.Kind,
                Occurrences = kv.Value.Count,
                Returns = returns.GetValueOrDefault(kv.Key),
                ReturnRatePercent = Pct(returns.GetValueOrDefault(kv.Key), kv.Value.Count)
            })
            .OrderByDescending(r => r.Returns)
            .ThenByDescending(r => r.ReturnRatePercent)
            .ToList();
    }

    private async Task<(List<ProcessBottleneckStep>, List<ProcessBottleneckParticipant>)> BuildBottlenecksAsync(
        List<StepRow> steps,
        List<ParticipantRow> participants,
        Dictionary<int, StepRow> stepById,
        Func<DateTime, DateTime, double> elapsed)
    {
        // Ожидание на этапе = активация участника → его решение. Автоакцепт тоже
        // считаем решением: это факт «этап простоял до таймаута».
        var byStep = new Dictionary<string, WaitAccumulator>();
        var byUser = new Dictionary<int, WaitAccumulator>();

        foreach (var p in participants)
        {
            if (p.ActivatedAt is not { } activated || p.ResAt is not { } resolvedAt) continue;
            var wait = elapsed(activated, resolvedAt);

            if (stepById.TryGetValue(p.RouteStepId, out var st))
            {
                var label = StepLabel(st.Kind, st.IsFinalMethodology);
                if (!byStep.TryGetValue(label, out var acc))
                {
                    acc = new WaitAccumulator { Kind = (int)st.Kind };
                    byStep[label] = acc;
                }
                acc.Waits.Add(wait);
                if (p.ResType == ResolutionType.AutoAccept) acc.Timeouts++;
            }

            if (p.UserId is { } uid)
            {
                if (!byUser.TryGetValue(uid, out var uacc))
                {
                    uacc = new WaitAccumulator();
                    byUser[uid] = uacc;
                }
                uacc.Waits.Add(wait);
            }
        }

        var bottleneckSteps = byStep
            .Select(kv =>
            {
                var sorted = kv.Value.Waits.OrderBy(x => x).ToList();
                return new ProcessBottleneckStep
                {
                    StepLabel = kv.Key,
                    StepKind = kv.Value.Kind,
                    Samples = sorted.Count,
                    AverageWaitHours = Math.Round(sorted.Average(), 1),
                    MedianWaitHours = Math.Round(Median(sorted), 1),
                    MaxWaitHours = Math.Round(sorted[^1], 1),
                    TimeoutRatePercent = Pct(kv.Value.Timeouts, sorted.Count)
                };
            })
            .OrderByDescending(s => s.AverageWaitHours)
            .ToList();

        var userIds = byUser.Keys.ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var bottleneckParticipants = byUser
            .Select(kv =>
            {
                var sorted = kv.Value.Waits.OrderBy(x => x).ToList();
                return new ProcessBottleneckParticipant
                {
                    UserId = kv.Key,
                    Label = names.GetValueOrDefault(kv.Key, $"#{kv.Key}"),
                    Samples = sorted.Count,
                    AverageWaitHours = Math.Round(sorted.Average(), 1),
                    MedianWaitHours = Math.Round(Median(sorted), 1)
                };
            })
            .OrderByDescending(p => p.AverageWaitHours)
            .Take(20)
            .ToList();

        return (bottleneckSteps, bottleneckParticipants);
    }

    public async Task<ProcessMiningFilterOptions> GetFilterOptionsAsync()
    {
        var docTypes = await (
            from i in _db.RouteInstances.AsNoTracking()
            join d in _db.Documents.AsNoTracking() on i.DocumentId equals d.Id
            where i.StartedAt != null
            select d.Type)
            .Distinct()
            .ToListAsync();

        var templateIds = await _db.RouteInstances.AsNoTracking()
            .Where(i => i.StartedAt != null && i.TemplateId != null)
            .Select(i => i.TemplateId!.Value)
            .Distinct()
            .ToListAsync();

        var templates = await _db.RouteTemplates.AsNoTracking()
            .Where(t => templateIds.Contains(t.Id))
            .Select(t => new ProcessMiningOption { Id = t.Id, Label = t.Name })
            .ToListAsync();

        return new ProcessMiningFilterOptions
        {
            DocumentTypes = docTypes
                .OrderBy(t => (int)t)
                .Select(t => new ProcessMiningOption { Id = (int)t, Label = DocTypeLabel(t) })
                .ToList(),
            Templates = templates.OrderBy(t => t.Label).ToList()
        };
    }

    private static ProcessMiningReport Empty(string unit) => new()
    {
        Overview = new ProcessMiningOverview(),
        Variants = new List<ProcessPathVariant>(),
        Rework = new List<ProcessReworkStep>(),
        BottleneckSteps = new List<ProcessBottleneckStep>(),
        BottleneckParticipants = new List<ProcessBottleneckParticipant>(),
        DurationUnit = unit
    };

    // ── подписи ──────────────────────────────────────────────────────────────

    private static string StepLabel(StepKind kind, bool isFinalMethodology)
    {
        if (isFinalMethodology) return "Финальный контроль";
        return kind switch
        {
            StepKind.Approval => "Согласование",
            StepKind.FinalControl => "Финальный контроль",
            StepKind.Signing => "Подписание",
            StepKind.Board => "Правление",
            _ => kind.ToString()
        };
    }

    private static string DocTypeLabel(DocumentType t) => t switch
    {
        DocumentType.Sz => "Служебная записка",
        DocumentType.Vnd => "ВНД",
        DocumentType.Tid => "Таблица изменений (ТИД)",
        DocumentType.Procurement => "Закупка",
        DocumentType.Contract => "Договор",
        DocumentType.Custom => "Настраиваемый тип",
        DocumentType.HrOrder => "Приказ по личному составу",
        DocumentType.PowerOfAttorney => "Доверенность",
        DocumentType.Correspondence => "Корреспонденция",
        DocumentType.Meeting => "Заседание",
        _ => t.ToString()
    };

    // ── математика ─────────────────────────────────────────────────────────────

    private static double Pct(int part, int total) =>
        total > 0 ? Math.Round(part * 100.0 / total, 1) : 0;

    private static double Median(List<double> sorted)
    {
        if (sorted.Count == 0) return 0;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    // ── проекции запросов ──────────────────────────────────────────────────────

    private sealed class InstanceRow
    {
        public int Id { get; init; }
        public int? TemplateId { get; init; }
        public RouteInstanceStatus Status { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? FinishedAt { get; init; }
        public DocumentType DocType { get; init; }
    }

    private sealed class StepRow
    {
        public int Id { get; init; }
        public int RouteInstanceId { get; init; }
        public int Order { get; init; }
        public StepKind Kind { get; init; }
        public bool IsFinalMethodology { get; init; }
        public DateTime? ActivatedAt { get; init; }
    }

    private sealed class ParticipantRow
    {
        public int RouteStepId { get; init; }
        public int? UserId { get; init; }
        public ParticipantState State { get; init; }
        public DateTime? ActivatedAt { get; init; }
        public ResolutionType? ResType { get; init; }
        public DateTime? ResAt { get; init; }
    }

    private sealed class VariantAccumulator
    {
        public List<string> Steps { get; init; } = new();
        public int Frequency { get; set; }
        public int ReworkCount { get; set; }
        public List<double> Durations { get; } = new();
    }

    private sealed class WaitAccumulator
    {
        public int Kind { get; init; }
        public int Timeouts { get; set; }
        public List<double> Waits { get; } = new();
    }
}
