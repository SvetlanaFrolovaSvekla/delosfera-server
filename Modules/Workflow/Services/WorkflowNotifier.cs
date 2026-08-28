using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

public interface IWorkflowNotifier
{
    /// <summary>Согласующему пришла задача по документу (GEN-12, SZ-03, PRC-23).</summary>
    Task TaskAssignedAsync(IEnumerable<int> participantIds);

    /// <summary>Срок этапа истёк: автоакцепт или эскалация — обе новости адресные.</summary>
    Task OverdueAsync(int participantId, bool escalated);

    /// <summary>Маршрут завершился: автор узнаёт результат, не заходя в систему.</summary>
    Task RouteFinishedAsync(int routeInstanceId, RouteInstanceStatus status, string? comment);
}

/// <summary>
/// Уведомления движка маршрутов.
///
/// Точка одна на все контуры: ВНД, служебные записки, закупки и договоры ходят по
/// одному движку, и адресные письма разумнее вешать на его события, а не повторять
/// в каждом контуре — иначе один из них неизбежно останется без оповещений.
///
/// Текст строится от документа, а не от участника маршрута: сотрудник должен понять
/// из письма, что именно от него хотят, ещё до входа в систему.
/// </summary>
public class WorkflowNotifier : IWorkflowNotifier
{
    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<WorkflowNotifier> _logger;

    public WorkflowNotifier(
        DelosferaDbContext db,
        INotificationService notifications,
        ILogger<WorkflowNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task TaskAssignedAsync(IEnumerable<int> participantIds)
    {
        var ids = participantIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var participants = await _db.RouteParticipants
            .Include(p => p.RouteStep!).ThenInclude(s => s.RouteInstance)
            .Where(p => ids.Contains(p.Id) && p.UserId != null)
            .ToListAsync();

        foreach (var participant in participants)
        {
            var instance = participant.RouteStep?.RouteInstance;
            if (instance is null) continue;

            var document = await LoadDocumentAsync(instance.DocumentId);
            if (document is null) continue;

            var deadline = participant.DueAt is { } due
                ? $" Срок: до {due.ToLocalTime():dd.MM.yyyy HH:mm}."
                : string.Empty;

            await SafeSendAsync(
                [participant.UserId!.Value],
                $"{Kind(document.Type)} на согласование: {Describe(document)}",
                $"На ваше согласование поступил документ «{document.Title}»" +
                $" ({StepTitle(participant.RouteStep!)}).{deadline}",
                document, NotificationSeverity.Urgent);
        }
    }

    public async Task OverdueAsync(int participantId, bool escalated)
    {
        var participant = await _db.RouteParticipants
            .Include(p => p.RouteStep!).ThenInclude(s => s.RouteInstance)
            .FirstOrDefaultAsync(p => p.Id == participantId);

        var instance = participant?.RouteStep?.RouteInstance;
        if (participant?.UserId is null || instance is null) return;

        var document = await LoadDocumentAsync(instance.DocumentId);
        if (document is null) return;

        // При эскалации адресат — не только просрочивший: смысл эскалации в том, что
        // о простое узнаёт автор документа и может вмешаться.
        var recipients = new List<int> {participant.UserId.Value};
        if (escalated) recipients.Add(document.AuthorId);

        var (title, body) = escalated
            ? ($"Просрочен этап согласования: {Describe(document)}",
               $"{StepTitle(participant.RouteStep!)} по документу «{document.Title}» просрочен " +
               "и передан на финальный контроль.")
            : ($"Автоакцепт по сроку: {Describe(document)}",
               $"Срок этапа по документу «{document.Title}» истёк ({StepTitle(participant.RouteStep!)}), " +
               "резолюция проставлена автоматически.");

        await SafeSendAsync(recipients, title, body, document, NotificationSeverity.Warning);
    }

    public async Task RouteFinishedAsync(int routeInstanceId, RouteInstanceStatus status, string? comment)
    {
        var instance = await _db.RouteInstances.FirstOrDefaultAsync(i => i.Id == routeInstanceId);
        if (instance is null) return;

        var document = await LoadDocumentAsync(instance.DocumentId);
        if (document is null) return;

        var (title, body, severity) = status switch
        {
            RouteInstanceStatus.Approved => (
                $"Согласование завершено: {Describe(document)}",
                $"Документ «{document.Title}» прошёл согласование полностью.",
                NotificationSeverity.Success),

            RouteInstanceStatus.Rejected => (
                $"Документ отклонён: {Describe(document)}",
                $"Документ «{document.Title}» отклонён." + Reason(comment),
                NotificationSeverity.Urgent),

            RouteInstanceStatus.OnRevision => (
                $"Возврат на доработку: {Describe(document)}",
                $"По документу «{document.Title}» есть замечания, требующие устранения." + Reason(comment),
                NotificationSeverity.Warning),

            RouteInstanceStatus.Interrupted => (
                $"Согласование прервано: {Describe(document)}",
                $"Маршрут согласования по документу «{document.Title}» остановлен." + Reason(comment),
                NotificationSeverity.Warning),

            // Промежуточные переходы автора не касаются: письмо на каждый шаг маршрута
            // быстро приучает не читать письма вовсе.
            _ => (string.Empty, string.Empty, NotificationSeverity.Info),
        };

        if (title.Length == 0) return;

        await SafeSendAsync(await OutcomeRecipientsAsync(document), title, body, document, severity);
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<Document?> LoadDocumentAsync(int documentId) =>
        await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);

    /// <summary>
    /// Кому сообщать об исходе согласования.
    ///
    /// Задание банка требует извещать не только автора: «уведомления должны
    /// отправляться всем — исполнителю, создателю, кто регистрирует, начальник СП».
    /// Прежде уходило одному автору, и начальник узнавал об отклонении документа
    /// своего подразделения от подчинённого, а делопроизводитель — когда документ
    /// не приходил на регистрацию.
    ///
    /// Это касается только исхода: завершения, отклонения, возврата, остановки.
    /// Промежуточные шаги остаются у участников — письмо на каждый шаг маршрута
    /// быстро приучает не читать письма вовсе.
    /// </summary>
    private async Task<List<int>> OutcomeRecipientsAsync(Document document)
    {
        var recipients = new HashSet<int> {document.AuthorId};

        // Начальник подразделения автора. Если автор сам начальник — он уже
        // в списке, и повтор отсеет HashSet.
        var headId = await _db.Users
            .Where(u => u.Id == document.AuthorId && u.OrgUnit != null)
            .Select(u => u.OrgUnit!.HeadUserId)
            .FirstOrDefaultAsync();

        if (headId is { } head) recipients.Add(head);

        // Делопроизводитель, зарегистрировавший записку. Поле есть только
        // у служебных записок: у прочих контуров регистрация устроена иначе.
        if (document.Type == DocumentType.Sz)
        {
            var registrarId = await _db.SzDocuments
                .Where(s => s.DocumentId == document.Id)
                .Select(s => s.RegisteredByUserId)
                .FirstOrDefaultAsync();

            if (registrarId is { } registrar) recipients.Add(registrar);
        }

        return recipients.ToList();
    }

    /// <summary>
    /// Сбой рассылки не должен ронять согласование: резолюция уже вынесена, и откат
    /// транзакции из-за недоступной почты испортил бы куда больше, чем неотправленное
    /// письмо. Поэтому ошибка пишется в журнал, а процесс идёт дальше.
    /// </summary>
    private async Task SafeSendAsync(
        List<int> userIds, string title, string body, Document document, NotificationSeverity severity)
    {
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = title,
                BodyRu = body,
                Category = NotificationCategory.Approval,
                Severity = severity,
                EntityType = document.Type.ToString(),
                EntityId = document.Id,
                Url = await ResolveUrlAsync(document),
                UserIds = userIds.Distinct().ToList(),
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Не удалось разослать уведомление по документу {DocumentId}: {Title}", document.Id, title);
        }
    }

    /// <summary>
    /// Название этапа. Своего заголовка у этапа нет, поэтому собираем из номера и вида —
    /// «этап 2, согласование» понятнее, чем просто порядковый номер.
    /// </summary>
    private static string StepTitle(RouteStep step)
    {
        var kind = step.Kind switch
        {
            StepKind.Approval => "согласование",
            StepKind.FinalControl => "финальный контроль",
            StepKind.Signing => "подписание",
            StepKind.Board => "рассмотрение Правлением",
            _ => "этап маршрута",
        };

        return $"этап {step.Order}, {kind}";
    }

    private static string Describe(Document document) =>
        string.IsNullOrWhiteSpace(document.RegNumber) ? document.Title : document.RegNumber;

    private static string Reason(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? string.Empty : $" Причина: {comment}";

    private static string Kind(DocumentType type) => type switch
    {
        DocumentType.Sz => "Служебная записка",
        DocumentType.Vnd => "ВНД",
        DocumentType.Tid => "ТИД",
        DocumentType.Procurement => "Заявка на закупку",
        DocumentType.Contract => "Договор",
        _ => "Документ",
    };

    /// <summary>
    /// Ссылка ведёт в карточку своего контура — письмо без неё бесполезно.
    ///
    /// Идентификатор карточки не совпадает с идентификатором документа: у служебной
    /// записки, заявки и договора своя запись, ссылающаяся на общий документ. Поэтому
    /// адрес собирается запросом, а не подстановкой document.Id.
    /// </summary>
    private async Task<string> ResolveUrlAsync(Document document)
    {
        switch (document.Type)
        {
            case DocumentType.Sz:
                var szId = await _db.SzDocuments
                    .Where(s => s.DocumentId == document.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync();
                return szId is null ? "/sz" : $"/sz/{szId}";

            case DocumentType.Procurement:
                var requestId = await _db.ProcurementRequests
                    .Where(r => r.DocumentId == document.Id).Select(r => (int?)r.Id).FirstOrDefaultAsync();
                return requestId is null ? "/prc" : $"/prc/{requestId}";

            case DocumentType.Contract:
                // Договор живёт панелью внутри карточки своей закупки — туда и ведём.
                var contractRequestId = await _db.ProcurementContracts
                    .Where(c => c.DocumentId == document.Id).Select(c => (int?)c.RequestId).FirstOrDefaultAsync();
                return contractRequestId is null ? "/prc" : $"/prc/{contractRequestId}";

            case DocumentType.Vnd:
            case DocumentType.Tid:
                return "/tasks";

            default:
                return "/inbox";
        }
    }
}
