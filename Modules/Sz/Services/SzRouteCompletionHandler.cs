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

    /// <summary>
    /// Тип задачи автору на устранение замечаний — по нему её находят и закрывают,
    /// когда записка снова уходит на согласование.
    /// </summary>
    public const string RemarksResolutionTask = "RemarksResolution";

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
        // По записке проходят два маршрута: согласование и подписание. Куда вести
        // дальше — зависит от того, какой из них завершился, а различает их текущий
        // статус записки: до регистрации она на согласовании, после — у подписанта.
        var подписание = sz.Document!.StatusCode == SzStatus.OnSigning;

        // Записки, заведённые до перестройки порядка, получили номер ещё до
        // согласования. Отправлять их «ждать регистрации» нельзя: регистрация
        // выдаст второй номер тому, что уже занесено в книгу под первым.
        var ужеЗарегистрирована = sz.Document.RegNumber is not null;

        var status = routeStatus switch
        {
            // Согласование пройдено — записка идёт на регистрацию: номер получает
            // то, с чем уже согласились. Кроме записок прежнего порядка: у них
            // номер уже есть, и они идут дальше сразу.
            RouteInstanceStatus.Approved when !подписание && !ужеЗарегистрирована
                => SzStatus.PendingRegistration,

            // Подписано — записка на исполнении: подписывает тот, кому она
            // адресована, и он же отписывает её исполнителям резолюцией.
            // Отдельного решения между подписью и резолюцией нет: это одно
            // действие одного человека, разнесённое на два только на бумаге.
            RouteInstanceStatus.Approved when подписание => SzStatus.OnExecution,

            // Подписания не было — записка идёт к адресату за решением по существу.
            RouteInstanceStatus.Approved when sz.AddresseeUserId is not null => SzStatus.OnAddresseeDecision,
            RouteInstanceStatus.Approved => SzStatus.OnExecution,

            RouteInstanceStatus.Rejected => SzStatus.Rejected,
            RouteInstanceStatus.OnRevision => SzStatus.OnRevision,
            RouteInstanceStatus.Running => подписание ? SzStatus.OnSigning : SzStatus.OnApproval,
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

        // Возврат на доработку — это работа автора: записка должна попасть в его
        // список задач, а не только в уведомления. Задача снимается, когда записка
        // уходит с доработки (повторное согласование, отзыв, брак).
        if (status == SzStatus.OnRevision)
            await CreateRevisionTaskAsync(sz);
        else
            await CloseRevisionTaskAsync(sz.DocumentId);
    }

    /// <summary>
    /// Задача автору на устранение замечаний в общий список задач. Одного уведомления
    /// мало: вернувшаяся записка иначе не видна автору среди задач, и по списку не
    /// понять, что он кому-то должен доработку.
    /// </summary>
    public async Task CreateRevisionTaskAsync(SzDocument sz)
    {
        var exists = await _db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.Type == RemarksResolutionTask
            && t.State == WorkflowTaskState.Open);

        // Повторный возврат на доработку приводит сюда снова — второй такой же задачи
        // быть не должно.
        if (exists) return;

        _db.WorkflowTasks.Add(new WorkflowTask
        {
            DocumentId = sz.DocumentId,
            SourceEntityId = sz.Id,
            AssigneeUserId = sz.Document!.AuthorId,
            Type = RemarksResolutionTask,
            // Срок доработки — срок исполнения записки: доработка и есть то, без чего
            // её нельзя двигать дальше.
            DueAt = sz.DueDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            State = WorkflowTaskState.Open,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Снимает задачу на доработку, когда записка ушла с доработки: повторно отправлена
    /// на согласование, отозвана или забракована. Без этого доработка висела бы в
    /// задачах автора и после того, как он её уже сдал.
    /// </summary>
    public async Task CloseRevisionTaskAsync(int documentId)
    {
        var open = await _db.WorkflowTasks
            .Where(t => t.DocumentId == documentId
                        && t.Type == RemarksResolutionTask
                        && t.State == WorkflowTaskState.Open)
            .ToListAsync();

        if (open.Count == 0) return;

        foreach (var task in open)
            task.State = WorkflowTaskState.Done;

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Задача адресату в общий список задач. Одного уведомления мало: уведомление
    /// читают и забывают, а записка остаётся ждать решения, и по списку задач не
    /// видно, что человек кому-то должен ответ.
    /// </summary>
    public async Task CreateAddresseeTaskAsync(SzDocument sz)
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
    public async Task NotifyAddresseeAsync(SzDocument sz, int actorUserId)
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
