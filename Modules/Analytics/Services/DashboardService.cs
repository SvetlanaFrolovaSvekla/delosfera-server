using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Analytics.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int userId);
}

/// <summary>
/// Сводка рабочего стола (GEN-15): что ждёт действия текущего пользователя во всех
/// контурах сразу — задачи маршрутов, записки, закупки и просрочки.
///
/// Задачи считаются с учётом замещения (GEN-14): пока замещение активно, задачи
/// отсутствующего входят в сводку замещающего, иначе на время отпуска документы
/// зависают без видимого владельца.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public DashboardService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(int userId)
    {
        var today = _clock.Today;
        var now = DateTime.UtcNow;

        var actingFor = await _db.Substitutions
            .Include(s => s.User)
            .Where(s => s.SubstituteUserId == userId && !s.IsCancelled
                        && s.StartsOn <= today && s.EndsOn >= today)
            .ToListAsync();

        var replacedBy = await _db.Substitutions
            .Include(s => s.SubstituteUser)
            .Where(s => s.UserId == userId && !s.IsCancelled
                        && s.StartsOn <= today && s.EndsOn >= today)
            .ToListAsync();

        // Свои задачи плюс задачи тех, кого сейчас замещаем.
        var assignees = actingFor.Select(s => s.UserId).Append(userId).Distinct().ToList();

        var openTasks = await _db.WorkflowTasks
            .Where(t => assignees.Contains(t.AssigneeUserId) && t.State == WorkflowTaskState.Open)
            .Select(t => new { t.DueAt })
            .ToListAsync();

        var overdueTasks = openTasks.Count(t => t.DueAt is { } due && due < now);

        // Записки, где пользователь — активный согласующий: связь идёт через маршрут,
        // у задачи собственной ссылки на документ нет.
        var szInbox = await (
            from p in _db.RouteParticipants
            join st in _db.RouteSteps on p.RouteStepId equals st.Id
            join ri in _db.RouteInstances on st.RouteInstanceId equals ri.Id
            join d in _db.Documents on ri.DocumentId equals d.Id
            where assignees.Contains(p.UserId ?? 0)
                  && p.State == ParticipantState.Active
                  && d.Type == DocumentType.Sz
            select d.Id).Distinct().CountAsync();

        var szOverdue = await _db.SzDocuments
            .Include(s => s.Document)
            .CountAsync(s => s.DueDate != null && s.DueDate < today
                             && s.Document!.StatusCode != SzStatus.Executed
                             && s.Document.StatusCode != SzStatus.Archived);

        var prcOnApproval = await _db.ProcurementRequests
            .CountAsync(r => r.Document!.StatusCode == ProcurementStatus.OnApproval);

        var prcInProcurement = await _db.ProcurementRequests
            .CountAsync(r => r.Document!.StatusCode == ProcurementStatus.InProcurement
                             || r.Document.StatusCode == ProcurementStatus.Approved);

        var summary = new DashboardSummaryDto
        {
            ActingFor = actingFor.Select(s => new ActiveSubstitutionDto
            {
                Id = s.Id,
                UserName = s.User?.FullName ?? "—",
                StartsOn = s.StartsOn,
                EndsOn = s.EndsOn,
                Reason = s.Reason,
            }).ToList(),
            ReplacedBy = replacedBy.Select(s => new ActiveSubstitutionDto
            {
                Id = s.Id,
                UserName = s.SubstituteUser?.FullName ?? "—",
                StartsOn = s.StartsOn,
                EndsOn = s.EndsOn,
                Reason = s.Reason,
            }).ToList(),
        };

        summary.Kpis.Add(new DashboardKpiDto
        {
            Code = "tasks",
            Label = "Мои задачи",
            Value = openTasks.Count,
            Note = overdueTasks > 0 ? $"из них просрочено: {overdueTasks}" : null,
            Tone = overdueTasks > 0 ? "danger" : "normal",
        });

        summary.Kpis.Add(new DashboardKpiDto
        {
            Code = "sz-inbox",
            Label = "Записки на согласовании у меня",
            Value = szInbox,
            Tone = szInbox > 0 ? "warning" : "normal",
        });

        summary.Kpis.Add(new DashboardKpiDto
        {
            Code = "sz-overdue",
            Label = "Записки с нарушением срока",
            Value = szOverdue,
            Note = szOverdue > 0 ? "норматив исполнения истёк" : null,
            Tone = szOverdue > 0 ? "danger" : "normal",
        });

        summary.Kpis.Add(new DashboardKpiDto
        {
            Code = "prc-approval",
            Label = "Заявки на закупку в согласовании",
            Value = prcOnApproval,
            Tone = "normal",
        });

        summary.Kpis.Add(new DashboardKpiDto
        {
            Code = "prc-active",
            Label = "Закупки в процедуре",
            Value = prcInProcurement,
            Tone = "normal",
        });

        return summary;
    }
}
