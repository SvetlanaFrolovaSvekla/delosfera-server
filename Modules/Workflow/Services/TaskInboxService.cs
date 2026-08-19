using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
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
            }).ToListAsync();

        var rows = routeRows.Concat(directRows).ToList();

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
                CreatedAt = r.CreatedAt,
            })
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
    }
}
