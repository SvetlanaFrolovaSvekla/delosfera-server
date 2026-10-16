using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
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

    /// <summary>
    /// Задача автору «Моя СЗ на согласовании»: записка зарегистрирована и ушла
    /// согласующим. Действия от автора не требует — это отслеживание, но в списке
    /// задач (и на главной) оно должно быть, иначе автор не видит, что записка в работе.
    /// Снимается, как только согласование закончилось (согласована, отклонена,
    /// возвращена на доработку, отозвана).
    /// </summary>
    public const string ApprovalTrackingTask = "SzApprovalTracking";

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    /// <summary>
    /// Провайдер нужен ради маршрута подписания: после согласования обработчик
    /// сам запускает маршрут подписи, а прямая зависимость от IRouteEngine
    /// замкнула бы кольцо — движок держит список обработчиков. Резолвим по месту.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    public SzRouteCompletionHandler(
        DelosferaDbContext db,
        IDocumentService documents,
        IAuditService audit,
        INotificationService notifications,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _notifications = notifications;
        _serviceProvider = serviceProvider;
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

        // Согласование закончилось (любым исходом) — задача автора «Моя СЗ на
        // согласовании» больше не нужна.
        if (routeStatus != RouteInstanceStatus.Running)
            await CloseApprovalTrackingTaskAsync(documentId);

        // Статус записки ведётся за маршрутом: иначе документ оставался бы «Зарегистрирован»,
        // пока согласующие уже вернули его на доработку.
        //
        // Согласование пройдено — записка не идёт сразу в исполнение: если у неё есть
        // адресат, решение по существу выносит он, и до этого исполнять нечего.
        // По записке проходят два маршрута: согласование и подписание. Куда вести
        // дальше — зависит от того, какой из них завершился, а различает их текущий
        // статус записки: до регистрации она на согласовании, после — у подписанта.
        // По записке проходят два маршрута — согласование и подписание. Какой из
        // них завершился, различает текущий статус: до подписи она «на согласовании»,
        // на подписи — «на подписи».
        var наПодписи = sz.Document!.StatusCode == SzStatus.OnSigning;

        // Согласование завершено — записка идёт на подпись руководству (адресату).
        // Регистрация проходит ДО согласования, поэтому следующий шаг не регистрация,
        // а подпись: здесь же запускаем маршрут подписи. Из обработчика это безопасно —
        // движок сохраняет маршрут согласования завершённым ещё ДО вызова обработчиков
        // (RouteEngine.AdvanceToNextStep), так что отдельный маршрут не реентрантен.
        // IRouteEngine достаём из провайдера: прямая зависимость замкнула бы кольцо,
        // ведь движок держит список обработчиков.
        // Подписант: по умолчанию записку подписывает тот, кому она адресована. Но если
        // автор явно назвал отдельного подписанта в форме, подписывает он — иначе поле
        // «Подписант» в карточке не значило бы ничего. Адресат при этом остаётся адресатом
        // (получает и исполняет), меняется лишь тот, кто ставит подпись.
        if (routeStatus == RouteInstanceStatus.Approved && !наПодписи
            && (sz.SignerUserId ?? sz.AddresseeUserId) is { } signer)
        {
            var engine = _serviceProvider.GetRequiredService<IRouteEngine>();
            var signing = await engine.InstantiateForSignerAsync(sz.DocumentId, signer);
            await engine.StartAsync(signing.Id, actorUserId);
            sz.Document.CurrentRouteInstanceId = signing.Id;

            await _documents.ChangeStatusAsync(documentId, SzStatus.OnSigning, actorUserId);
            await _audit.LogAsync("Sz", sz.Id, "SentToSigner", actorUserId,
                new { signer, routeInstanceId = signing.Id });
            return;
        }

        var status = routeStatus switch
        {
            // Подписано отдельным подписантом — записка идёт к адресату за решением по
            // существу. Раньше она в этом случае уходила сразу на исполнение: адресат
            // не получал ни задачи, ни уведомления, и записка, адресованная ему,
            // исполнялась мимо него.
            RouteInstanceStatus.Approved when наПодписи && NeedsSeparateAddresseeDecision(sz)
                => SzStatus.OnAddresseeDecision,

            // Подписал сам адресат — записка на исполнении: он же отписывает её
            // исполнителям резолюцией. Отдельного решения между подписью и резолюцией
            // нет — это одно действие одного человека.
            RouteInstanceStatus.Approved when наПодписи => SzStatus.OnExecution,

            // Подписанта нет (адресат не указан) — записка идёт к адресату за решением
            // по существу, а без адресата сразу на исполнение.
            RouteInstanceStatus.Approved when sz.AddresseeUserId is not null => SzStatus.OnAddresseeDecision,
            RouteInstanceStatus.Approved => SzStatus.OnExecution,

            RouteInstanceStatus.Rejected => SzStatus.Rejected,
            RouteInstanceStatus.OnRevision => SzStatus.OnRevision,
            RouteInstanceStatus.Running => наПодписи ? SzStatus.OnSigning : SzStatus.OnApproval,
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
    /// Подписант назван отдельно и это не адресат: подпись поставил другой человек,
    /// и решение по существу за адресатом ещё не вынесено. Когда подписанта не
    /// назвали, подписывает сам адресат (см. выбор подписанта выше) — тогда
    /// отдельного шага нет.
    /// </summary>
    private static bool NeedsSeparateAddresseeDecision(SzDocument sz) =>
        sz.AddresseeUserId is { } addressee
        && sz.SignerUserId is { } signer
        && signer != addressee;

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
    /// Задача автору «Моя СЗ на согласовании» — ставится, когда записку зарегистрировали
    /// и отправили на согласование (и при повторной отправке с доработки).
    /// </summary>
    public async Task CreateApprovalTrackingTaskAsync(SzDocument sz)
    {
        var exists = await _db.WorkflowTasks.AnyAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.Type == ApprovalTrackingTask
            && t.State == WorkflowTaskState.Open);
        if (exists) return;

        _db.WorkflowTasks.Add(new WorkflowTask
        {
            DocumentId = sz.DocumentId,
            SourceEntityId = sz.Id,
            AssigneeUserId = sz.Document!.AuthorId,
            Type = ApprovalTrackingTask,
            // Срока у автора нет: пока записка у согласующих, ему нечего просрочить.
            DueAt = null,
            State = WorkflowTaskState.Open,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>Снимает задачу автора «Моя СЗ на согласовании».</summary>
    public async Task CloseApprovalTrackingTaskAsync(int documentId)
    {
        var open = await _db.WorkflowTasks
            .Where(t => t.DocumentId == documentId
                        && t.Type == ApprovalTrackingTask
                        && t.State == WorkflowTaskState.Open)
            .ToListAsync();

        if (open.Count == 0) return;

        foreach (var task in open)
        {
            task.State = WorkflowTaskState.Done;
            task.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Системное уведомление делопроизводителям (все активные пользователи с правом
    /// регистрации СЗ): появилась записка, ожидающая регистрации. Без него новая задача
    /// у Сектора делопроизводства появлялась молча. Сбой рассылки не должен откатывать
    /// отправку — вызывающий оборачивает вызов в try/catch.
    /// </summary>
    public async Task NotifyClerksPendingRegistrationAsync(SzDocument sz, int actorUserId)
    {
        var registerCode = (int)PermissionCode.RegisterSz;
        var clerkIds = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.PermissionCodes.Contains(registerCode)))
            .Select(u => u.Id)
            .ToListAsync();

        // Автор сам себя не уведомляет, если он же и делопроизводитель.
        clerkIds.Remove(actorUserId);
        if (clerkIds.Count == 0) return;

        var title = sz.Document?.Title
                    ?? await _db.Documents.AsNoTracking()
                        .Where(d => d.Id == sz.DocumentId).Select(d => d.Title).FirstOrDefaultAsync()
                    ?? "";
        var authorName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId).Select(u => u.FullName).FirstOrDefaultAsync();
        var byRu = string.IsNullOrWhiteSpace(authorName) ? "" : $" Отправил: {authorName}.";
        var byEn = string.IsNullOrWhiteSpace(authorName) ? "" : $" Submitted by {authorName}.";
        var byKg = string.IsNullOrWhiteSpace(authorName) ? "" : $" Жөнөттү: {authorName}.";

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = "Новая СЗ на регистрацию",
            TitleKg = "Каттоого жаңы кызматтык кат",
            TitleEn = "New memo awaiting registration",
            BodyRu = $"Служебная записка «{title}» ожидает регистрации.{byRu}",
            BodyKg = $"«{title}» кызматтык каты каттоону күтүүдө.{byKg}",
            BodyEn = $"Memo “{title}” is awaiting registration.{byEn}",
            Category = NotificationCategory.Sz,
            Severity = NotificationSeverity.Info,
            EntityType = "Sz",
            EntityId = sz.Id,
            Url = $"/sz/{sz.Id}",
            UserIds = clerkIds,
        }, actorUserId);
    }

    /// <summary>
    /// Системное уведомление автору: записку зарегистрировали (присвоен номер) и
    /// отправили на согласование. Сбой рассылки не должен откатывать регистрацию —
    /// вызывающий зовёт это уже после фиксации транзакции.
    /// </summary>
    public async Task NotifyAuthorRegisteredAsync(SzDocument sz, int actorUserId)
    {
        // Номер берём из базы: регистрация могла записать его в обход отслеживаемой сущности.
        var number = await _db.Documents.AsNoTracking()
            .Where(d => d.Id == sz.DocumentId)
            .Select(d => d.RegNumber)
            .FirstOrDefaultAsync() ?? sz.Document!.RegNumber;
        var numberRu = string.IsNullOrWhiteSpace(number) ? "" : $" № {number}";
        var dateRu = sz.RegisteredOn is { } regOn ? $" от {regOn:dd.MM.yyyy}" : "";

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = "Служебная записка зарегистрирована",
            TitleKg = "Кызматтык кат катталды",
            TitleEn = "Memo registered",
            BodyRu = $"«{sz.Document!.Title}» зарегистрирована{numberRu}{dateRu} и отправлена на согласование.",
            BodyKg = $"«{sz.Document.Title}» катталды{numberRu}{dateRu} жана макулдашууга жөнөтүлдү.",
            BodyEn = $"“{sz.Document.Title}” has been registered{(string.IsNullOrWhiteSpace(number) ? "" : $" as No. {number}")} and sent for approval.",
            Category = NotificationCategory.Sz,
            Severity = NotificationSeverity.Success,
            EntityType = "Sz",
            EntityId = sz.Id,
            Url = $"/sz/{sz.Id}",
            UserIds = [sz.Document.AuthorId],
        }, actorUserId);
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
        {
            task.State = WorkflowTaskState.Done;
            task.CompletedAt = DateTime.UtcNow;
        }

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
            TitleEn = "Memo awaits your decision",
            BodyRu = $"«{sz.Document!.Title}» согласована. Вынесите решение по существу.",
            BodyKg = $"«{sz.Document.Title}» макулдашылды. Маңызы боюнча чечим чыгарыңыз.",
            BodyEn = $"“{sz.Document.Title}” has been approved. Please make a decision on the merits.",
            Category = NotificationCategory.Sz,
            EntityType = "Sz",
            EntityId = sz.Id,
            Url = $"/sz/{sz.Id}",
            UserIds = [sz.AddresseeUserId!.Value],
        }, actorUserId);
    }
}
