using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Реакция контура СЗ на события маршрута: статус записки ведётся за маршрутом
/// (согласован → на исполнении, отклонён → забракована, замечания → на доработке).
///
/// Обработчик намеренно не зависит от ISzService: движок сам получает обработчики,
/// а SzService зависит от движка — через сервис контура получилось бы кольцо в DI.
/// </summary>
public class SzRouteCompletionHandler : IRouteCompletionHandler
{
    /// <summary>Тип задачи адресата — по нему её находят и закрывают при решении.</summary>
    public const string AddresseeDecisionTask = "AddresseeDecision";

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public SzRouteCompletionHandler(
        DelosferaDbContext db,
        IDocumentService documents,
        IAuditService audit,
        INotificationService notifications)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _notifications = notifications;
    }

    public Task OnRouteApprovedAsync(int routeInstanceId, int documentId, int actorUserId) =>
        SyncAsync(documentId, RouteInstanceStatus.Approved, actorUserId);

    public Task OnRouteStatusChangedAsync(
        int routeInstanceId, int documentId, RouteInstanceStatus status, int actorUserId) =>
        SyncAsync(documentId, status, actorUserId);

    private async Task SyncAsync(int documentId, RouteInstanceStatus routeStatus, int actorUserId)
    {
        // Маршруты других контуров сюда не относятся — сверяемся с типом документа.
        var sz = await _db.SzDocuments
            .Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.Document!.Type == DocumentType.Sz);

        if (sz is null) return;

        // Статус записки ведётся за маршрутом: иначе документ оставался бы «Зарегистрирован»,
        // пока согласующие уже вернули его на доработку.
        //
        // Согласование пройдено — записка не идёт сразу в исполнение: если у неё есть
        // адресат, решение по существу выносит он, и до этого исполнять нечего.
        var status = routeStatus switch
        {
            RouteInstanceStatus.Approved when sz.AddresseeUserId is not null => SzStatus.OnAddresseeDecision,
            RouteInstanceStatus.Approved => SzStatus.OnExecution,
            RouteInstanceStatus.Rejected => SzStatus.Rejected,
            RouteInstanceStatus.OnRevision => SzStatus.OnRevision,
            RouteInstanceStatus.Running => SzStatus.Registered,
            _ => null
        };
        if (status is null || sz.Document!.StatusCode == status) return;

        await _documents.ChangeStatusAsync(documentId, status, actorUserId);
        await _audit.LogAsync("Sz", sz.Id, "StatusFromRoute", actorUserId,
            new { route = routeStatus.ToString(), status });

        if (status == SzStatus.OnAddresseeDecision)
        {
            await CreateAddresseeTaskAsync(sz);
            await NotifyAddresseeAsync(sz, actorUserId);
        }
    }

    /// <summary>
    /// Задача адресату в общий список задач. Одного уведомления мало: уведомление
    /// читают и забывают, а записка остаётся ждать решения, и по списку задач не
    /// видно, что человек кому-то должен ответ.
    /// </summary>
    private async Task CreateAddresseeTaskAsync(SzDocument sz)
    {
        var exists = await _db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.Type == AddresseeDecisionTask
            && t.State == WorkflowTaskState.Open);

        // Повторный круг согласования приводит сюда снова — второй такой же задачи
        // быть не должно.
        if (exists) return;

        _db.WorkflowTasks.Add(new WorkflowTask
        {
            DocumentId = sz.DocumentId,
            SourceEntityId = sz.Id,
            AssigneeUserId = sz.AddresseeUserId!.Value,
            Type = AddresseeDecisionTask,
            // Срок решения — срок исполнения записки: решение адресата и есть то,
            // после чего её вообще можно исполнять.
            DueAt = sz.DueDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            State = WorkflowTaskState.Open,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Адресат узнаёт о записке только из уведомления: в его задачах она не появляется —
    /// согласующим он не был.
    /// </summary>
    private async Task NotifyAddresseeAsync(SzDocument sz, int actorUserId)
    {
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = "Служебная записка ждёт вашего решения",
            TitleKg = "Кызматтык кат чечимиңизди күтүүдө",
            BodyRu = $"«{sz.Document!.Title}» согласована. Вынесите решение по существу.",
            BodyKg = $"«{sz.Document.Title}» макулдашылды. Маңызы боюнча чечим чыгарыңыз.",
            Category = NotificationCategory.Approval,
            EntityType = "Sz",
            EntityId = sz.Id,
            Url = $"/sz/{sz.Id}",
            UserIds = [sz.AddresseeUserId!.Value],
        }, actorUserId);
    }
}
