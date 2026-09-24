using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO.Response.Sla;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Analytics.Services;

public interface ISlaAnalyticsService
{
    /// <summary>Сводка соблюдения сроков по всем контурам (СК-2).</summary>
    Task<SlaOverviewResponse> GetOverviewAsync();

    /// <summary>Сотрудники с наибольшим числом просроченных задач.</summary>
    Task<List<SlaViolatorItem>> GetTopOverdueAsync(int top = 15);
}

/// <summary>
/// SLA-аналитика: сроки по всем контурам сразу, для руководства (СК-2).
///
/// Источники те же, что у единого реестра задач (WorkflowTask по записок и закупкам,
/// VndApprovalStage по согласованию ВНД, AcknowledgementEntry по ознакомлению), но
/// считаются по всей организации, а не по одному пользователю: рабочий стол отвечает
/// «что у меня», этот срез — «где по банку горят сроки».
/// </summary>
public class SlaAnalyticsService : ISlaAnalyticsService
{
    private readonly DelosferaDbContext _db;

    public SlaAnalyticsService(DelosferaDbContext db)
    {
        _db = db;
    }

    /// <summary>Открытая задача одного контура: код контура и срок (может отсутствовать).</summary>
    private readonly record struct SlaItem(string Contour, string Label, DateTime? Due);

    public async Task<SlaOverviewResponse> GetOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var soon = now.AddHours(24);

        var items = await CollectAsync();

        static double Compliance(int open, int overdue) =>
            open == 0 ? 100 : Math.Round((open - overdue) * 100.0 / open, 1);

        var contours = items
            .GroupBy(i => (i.Contour, i.Label))
            .Select(g =>
            {
                var open = g.Count();
                var overdue = g.Count(i => i.Due is { } d && d < now);
                var dueSoon = g.Count(i => i.Due is { } d && d >= now && d <= soon);
                return new SlaContourRow
                {
                    Contour = g.Key.Contour,
                    Label = g.Key.Label,
                    Open = open,
                    Overdue = overdue,
                    DueSoon = dueSoon,
                    CompliancePercent = Compliance(open, overdue),
                };
            })
            // Худшие по соблюдению — наверх: руководителю важнее контур, где рвутся сроки.
            .OrderByDescending(c => c.Overdue)
            .ThenByDescending(c => c.Open)
            .ToList();

        var totalOpen = contours.Sum(c => c.Open);
        var totalOverdue = contours.Sum(c => c.Overdue);

        return new SlaOverviewResponse
        {
            OpenTasks = totalOpen,
            Overdue = totalOverdue,
            DueSoon = contours.Sum(c => c.DueSoon),
            CompliancePercent = Compliance(totalOpen, totalOverdue),
            Contours = contours,
        };
    }

    public async Task<List<SlaViolatorItem>> GetTopOverdueAsync(int top = 15)
    {
        var now = DateTime.UtcNow;

        // По каждому источнику — (исполнитель, срок). Считаем открытые и просроченные
        // на человека; людей без просрочек в топ не берём.
        var owners = new List<(int UserId, DateTime? Due)>();

        var wfRows = await (
            from t in _db.WorkflowTasks
            where t.State == WorkflowTaskState.Open
            select new { t.AssigneeUserId, t.DueAt }).ToListAsync();
        owners.AddRange(wfRows.Select(x => (x.AssigneeUserId, x.DueAt)));

        var vndStages = await OpenVndStagesAsync();
        owners.AddRange(vndStages.Select(s => (s.ApproverUserId, StageDue(s))));

        var ackOwners = await (
            from e in _db.AcknowledgementEntries
            join s in _db.AcknowledgementSheets on e.SheetId equals s.Id
            where e.State == AcknowledgementState.Pending && s.ClosedAt == null
            select new { e.UserId, s.DueDate })
            .ToListAsync();
        owners.AddRange(ackOwners.Select(a => (a.UserId, AckDue(a.DueDate))));

        var perUser = owners
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Open = g.Count(),
                Overdue = g.Count(o => o.Due is { } d && d < now),
            })
            .Where(x => x.Overdue > 0)
            .OrderByDescending(x => x.Overdue)
            .ThenByDescending(x => x.Open)
            .Take(top)
            .ToList();

        if (perUser.Count == 0) return [];

        var userIds = perUser.Select(x => x.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, OrgUnitTitle = u.OrgUnit != null ? u.OrgUnit.TitleRu : null })
            .ToDictionaryAsync(u => u.Id, u => u);

        return perUser.Select(x =>
        {
            var user = users.GetValueOrDefault(x.UserId);
            return new SlaViolatorItem
            {
                UserId = x.UserId,
                FullName = user?.FullName ?? $"#{x.UserId}",
                OrgUnitLabel = user?.OrgUnitTitle,
                Open = x.Open,
                Overdue = x.Overdue,
            };
        }).ToList();
    }

    /// <summary>Собирает открытые задачи всех контуров в единый список со сроком.</summary>
    private async Task<List<SlaItem>> CollectAsync()
    {
        var items = new List<SlaItem>();

        // WorkflowTask (записки, закупки): контур — тип документа, через маршрут либо
        // напрямую (у задач вне маршрута документ указан прямо).
        var routeRows = await (
            from t in _db.WorkflowTasks
            join p in _db.RouteParticipants on t.RouteParticipantId equals p.Id
            join st in _db.RouteSteps on p.RouteStepId equals st.Id
            join ri in _db.RouteInstances on st.RouteInstanceId equals ri.Id
            join d in _db.Documents on ri.DocumentId equals d.Id
            where t.State == WorkflowTaskState.Open
            select new { d.Type, t.DueAt }).ToListAsync();

        var directRows = await (
            from t in _db.WorkflowTasks
            join d in _db.Documents on t.DocumentId equals d.Id
            where t.State == WorkflowTaskState.Open && t.RouteParticipantId == null
            select new { d.Type, t.DueAt }).ToListAsync();

        foreach (var r in routeRows.Concat(directRows))
            items.Add(new SlaItem(r.Type.ToString(), DocTypeLabel(r.Type), r.DueAt));

        // ВНД-согласование: открытые этапы, где решение сейчас за согласующим.
        foreach (var s in await OpenVndStagesAsync())
            items.Add(new SlaItem("Vnd", "ВНД", StageDue(s)));

        // Ознакомление: незакрытые листы, где сотрудник ещё не расписался.
        var ackRows = await (
            from e in _db.AcknowledgementEntries
            join s in _db.AcknowledgementSheets on e.SheetId equals s.Id
            where e.State == AcknowledgementState.Pending && s.ClosedAt == null
            select new { s.DueDate }).ToListAsync();

        foreach (var a in ackRows)
            items.Add(new SlaItem("Acknowledgement", "Ознакомление", AckDue(a.DueDate)));

        return items;
    }

    /// <summary>Этап ВНД, где решение сейчас за пользователем, — по всем трём фазам.</summary>
    private Task<List<VndStageDue>> OpenVndStagesAsync() =>
        _db.VndApprovalStages
            .Where(s =>
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Primary
                 && s.PrimaryDecision == ApprovalStageDecision.Pending)
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Repeated
                 && s.ParticipatesInRepeat
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending))
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.FinalHold
                 && (s.FinalHoldDecision == null || s.FinalHoldDecision == ApprovalStageDecision.Pending)))
            .Select(s => new VndStageDue
            {
                ApproverUserId = s.ApproverUserId,
                Status = s.ApprovalProcess!.Status,
                PrimaryDeadlineAt = s.ApprovalProcess!.PrimaryDeadlineAt,
                RepeatDeadlineAt = s.ApprovalProcess!.RepeatDeadlineAt,
                FinalHoldDeadlineAt = s.ApprovalProcess!.FinalHoldDeadlineAt,
            })
            .ToListAsync();

    // Срок берётся по текущей фазе. Дедлайны хранятся в процессе (с учётом рабочего
    // календаря ВНД - см. VndApprovalDeadlines), пересчитывать "старт + минуты" нельзя.
    private static DateTime? StageDue(VndStageDue s) => s.Status switch
    {
        ApprovalProcessStatus.Primary => s.PrimaryDeadlineAt,
        ApprovalProcessStatus.Repeated => s.RepeatDeadlineAt,
        ApprovalProcessStatus.FinalHold => s.FinalHoldDeadlineAt,
        _ => null,
    };

    // Срок листа задан датой без времени: нарушенным считаем после конца дня.
    private static DateTime? AckDue(DateOnly? d) => d?.ToDateTime(new TimeOnly(23, 59, 59));

    private static string DocTypeLabel(DocumentType type) => type switch
    {
        DocumentType.Sz => "Служебные записки",
        DocumentType.Vnd => "ВНД",
        DocumentType.Tid => "ТИД",
        DocumentType.Procurement => "Закупки",
        DocumentType.Contract => "Договоры",
        _ => type.ToString(),
    };

    /// <summary>Поля процесса ВНД, из которых считается срок этапа.</summary>
    private sealed class VndStageDue
    {
        public int ApproverUserId { get; init; }
        public ApprovalProcessStatus Status { get; init; }
        public DateTime PrimaryDeadlineAt { get; init; }
        public DateTime? RepeatDeadlineAt { get; init; }
        public DateTime? FinalHoldDeadlineAt { get; init; }
    }
}
