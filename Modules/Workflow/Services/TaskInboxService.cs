using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

public interface ITaskInboxService
{
    /// <summary>Открытые задачи пользователя по всем контурам (GEN-11).</summary>
    Task<TaskInboxDto> GetAsync(int userId, string? documentType = null);
}

/// <summary>
/// Сводный реестр задач. Задачи движка не знают о контурах, поэтому документ
/// достаётся через цепочку участник → этап → маршрут → документ: без неё в списке
/// были бы безымянные строки «Approval», по которым не понять, что открывать.
///
/// Включает задачи тех, кого пользователь замещает (GEN-14), с пометкой, за кого
/// именно они выполняются.
/// </summary>
public class TaskInboxService : ITaskInboxService
{
    private readonly DelosferaDbContext _db;
    private readonly ISubstitutionService _substitutions;
    private readonly IBankClock _clock;

    public TaskInboxService(DelosferaDbContext db, ISubstitutionService substitutions, IBankClock clock)
    {
        _db = db;
        _substitutions = substitutions;
        _clock = clock;
    }

    public async Task<TaskInboxDto> GetAsync(int userId, string? documentType = null)
    {
        var actingFor = await _substitutions.GetActingForUserIdsAsync(userId);
        var assignees = actingFor.Append(userId).Distinct().ToList();

        var names = await _db.Users
            .Where(u => actingFor.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        // Задачи согласования: документ достаётся через цепочку участник → этап → маршрут.
        var routeRows = await (
            from t in _db.WorkflowTasks
            join p in _db.RouteParticipants on t.RouteParticipantId equals p.Id
            join st in _db.RouteSteps on p.RouteStepId equals st.Id
            join ri in _db.RouteInstances on st.RouteInstanceId equals ri.Id
            join d in _db.Documents on ri.DocumentId equals d.Id
            where assignees.Contains(t.AssigneeUserId) && t.State == WorkflowTaskState.Open
            select new Row
            {
                Id = t.Id,
                ParticipantId = p.Id,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = st.Order,
                Kind = (StepKind?)st.Kind,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DelegatedByUserId = t.DelegatedByUserId,
            }).ToListAsync();

        // Задачи контура — решение адресата, поручение: маршрута за ними нет, документ
        // указан напрямую. Без них список задач показывал бы только согласования, а
        // работа, выданная резолюцией, была бы видна лишь внутри своего раздела.
        var directRows = await (
            from t in _db.WorkflowTasks
            join d in _db.Documents on t.DocumentId equals d.Id
            where assignees.Contains(t.AssigneeUserId)
               && t.State == WorkflowTaskState.Open
               && t.RouteParticipantId == null
            select new Row
            {
                Id = t.Id,
                ParticipantId = null,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = null,
                Kind = null,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DelegatedByUserId = t.DelegatedByUserId,
            }).ToListAsync();

        // Ознакомление: лист ознакомления живёт вне движка задач — своя таблица без
        // маршрута. Без него сотрудник видит поручения и согласования, но не листы, с
        // которыми обязан расписаться, и «я этого не видел» ловится только вручную.
        var ackRows = await (
            from e in _db.AcknowledgementEntries
            join s in _db.AcknowledgementSheets on e.SheetId equals s.Id
            where assignees.Contains(e.UserId)
               && e.State == AcknowledgementState.Pending
               && s.ClosedAt == null
            select new
            {
                e.Id,
                e.UserId,
                e.CreatedAt,
                Instruction = s.Instruction,
                s.DueDate,
                s.DocumentId,
                DocTitle = s.Document != null ? s.Document.Title : null,
            }).ToListAsync();

        // ВНД-согласование: у нормативки свой контур согласования (VndApprovalProcess/
        // Stage), не движок задач. В единый список берём только этапы, где решение сейчас
        // за пользователем — все три фазы: первичная, повторная, финальная выдержка.
        // Актуализация и консолидация остаются в своей вкладке: там логика цикла, а не
        // одна задача согласования (см. VND TasksService).
        var vndStages = await _db.VndApprovalStages
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Vnd)
            .Where(s => assignees.Contains(s.ApproverUserId))
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
            .ToListAsync();

        var rows = routeRows.Concat(directRows).ToList();

        // Имена тех, кто делегировал задачи (СК-3): в реестре у делегата стоит «от кого».
        var delegatedByIds = rows
            .Where(r => r.DelegatedByUserId is not null)
            .Select(r => r.DelegatedByUserId!.Value)
            .Distinct()
            .ToList();

        var delegatedByNames = delegatedByIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users
                .Where(u => delegatedByIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

        // Карточки контуров открываются по своему идентификатору, а не по документу:
        // /sz/{szId}, /prc/{requestId}. Без этой подстановки задача уводила на чужую
        // карточку с тем же числом.
        var documentIds = rows.Select(r => r.DocumentId).Distinct().ToList();

        var entityIds = (await _db.SzDocuments
                .Where(x => documentIds.Contains(x.DocumentId))
                .Select(x => new {x.DocumentId, EntityId = x.Id})
                .ToListAsync())
            .Concat(await _db.ProcurementRequests
                .Where(x => documentIds.Contains(x.DocumentId))
                .Select(x => new {x.DocumentId, EntityId = x.Id})
                .ToListAsync())
            .ToDictionary(x => x.DocumentId, x => x.EntityId);

        var now = DateTime.UtcNow;

        // Срок листа задан датой без времени: считаем просроченным после конца дня,
        // а не с полуночи — иначе «до 14-го» гасло бы утром 14-го.
        static DateTime? AckDue(DateOnly? d) => d?.ToDateTime(new TimeOnly(23, 59, 59));

        var ackTasks = ackRows.Select(a => new InboxTaskDto
        {
            TaskId = a.Id,
            ParticipantId = null,
            // Лист открывается на общей странице ознакомления, не по своей карточке:
            // отдельного экрана листа нет, ссылка ведёт в раздел (см. taskLink).
            DocumentId = a.DocumentId ?? 0,
            EntityId = null,
            RegNumber = null,
            DocumentTitle = a.DocTitle ?? a.Instruction ?? "Ознакомление с документом",
            DocumentType = "Acknowledgement",
            DocumentTypeTitle = "Ознакомление",
            TaskType = "Ознакомление",
            StepOrder = null,
            StepKind = null,
            DueAt = AckDue(a.DueDate),
            IsOverdue = AckDue(a.DueDate) is { } due && due < now,
            OnBehalfOf = a.UserId != userId && names.TryGetValue(a.UserId, out var an) ? an : null,
            CreatedAt = a.CreatedAt,
        });

        var vndTasks = vndStages.Select(s =>
        {
            var p = s.ApprovalProcess!;
            // Срок берём по текущей фазе: у повторной и финальной он свой, и показать
            // срок первичной на финальной выдержке — сбить с толку.
            DateTime? due = p.Status switch
            {
                ApprovalProcessStatus.Primary => p.PrimaryDeadlineAt,
                ApprovalProcessStatus.Repeated => p.RepeatDeadlineAt,
                ApprovalProcessStatus.FinalHold => p.FinalHoldDeadlineAt,
                _ => null,
            };
            return new InboxTaskDto
            {
                TaskId = s.Id,
                ParticipantId = null,
                // Карточка ВНД открывается по своему id: /base-vnd/{vndId} (см. taskLink).
                DocumentId = 0,
                EntityId = p.VndId,
                RegNumber = p.Vnd!.Code,
                DocumentTitle = p.Vnd!.TitleRu,
                DocumentType = "Vnd",
                DocumentTypeTitle = "ВНД",
                TaskType = "Согласование",
                StepOrder = null,
                StepKind = null,
                DueAt = due,
                IsOverdue = due is { } d && d < now,
                OnBehalfOf = s.ApproverUserId != userId && names.TryGetValue(s.ApproverUserId, out var vn) ? vn : null,
                CreatedAt = p.PrimaryStartedAt,
            };
        });

        var tasks = rows
            .Select(r => new InboxTaskDto
            {
                TaskId = r.Id,
                ParticipantId = r.ParticipantId,
                DocumentId = r.DocumentId,
                EntityId = entityIds.TryGetValue(r.DocumentId, out var entityId) ? entityId : null,
                RegNumber = r.RegNumber,
                DocumentTitle = r.Title,
                DocumentType = r.DocType.ToString(),
                DocumentTypeTitle = DocumentTypeTitle(r.DocType),
                TaskType = TaskTypeTitle(r.TaskType),
                StepOrder = r.Order,
                StepKind = r.Kind?.ToString(),
                DueAt = r.DueAt,
                IsOverdue = r.DueAt is { } due && due < now,
                OnBehalfOf = r.AssigneeUserId != userId && names.TryGetValue(r.AssigneeUserId, out var name)
                    ? name
                    : null,
                DelegatedBy = r.DelegatedByUserId is { } delegId
                              && delegatedByNames.TryGetValue(delegId, out var delegName)
                    ? delegName
                    : null,
                CreatedAt = r.CreatedAt,
            })
            .Concat(ackTasks)
            .Concat(vndTasks)
            .Where(t => documentType is null || t.DocumentType == documentType)
            // Просроченные наверх, затем по сроку: реестр должен начинаться с того,
            // что горит, а не с того, что пришло первым.
            .OrderByDescending(t => t.IsOverdue)
            .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
            .ToList();

        return new TaskInboxDto
        {
            Tasks = tasks,
            Total = tasks.Count,
            Overdue = tasks.Count(t => t.IsOverdue),
            Delegated = tasks.Count(t => t.OnBehalfOf is not null),
        };
    }

    private static string DocumentTypeTitle(DocumentType type) => type switch
    {
        DocumentType.Sz => "Служебная записка",
        DocumentType.Vnd => "ВНД",
        DocumentType.Tid => "ТИД",
        DocumentType.Procurement => "Закупка",
        DocumentType.Contract => "Договор",
        _ => type.ToString(),
    };

    private static string TaskTypeTitle(string type) => type switch
    {
        "Approval" => "Согласование",
        "RemarksResolution" => "Устранение замечаний",
        "AddresseeDecision" => "Решение по записке",
        "Assignment" => "Поручение",
        _ => type,
    };

    /// <summary>
    /// Строка задачи до сборки ответа. Общий тип нужен, чтобы задачи маршрута и
    /// задачи контура собирались в один список.
    /// </summary>
    private sealed class Row
    {
        public int Id { get; init; }
        public int? ParticipantId { get; init; }
        public int AssigneeUserId { get; init; }
        public required string TaskType { get; init; }
        public DateTime? DueAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public int? Order { get; init; }
        public StepKind? Kind { get; init; }
        public int DocumentId { get; init; }
        public string? RegNumber { get; init; }
        public required string Title { get; init; }
        public DocumentType DocType { get; init; }
        public int? DelegatedByUserId { get; init; }
    }
}
