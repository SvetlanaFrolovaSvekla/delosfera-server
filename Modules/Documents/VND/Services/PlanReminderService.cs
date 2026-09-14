using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IPlanReminderService
{
    /// <summary>Напоминания по плану актуализации на указанную дату (PLN-04).</summary>
    Task<int> SendAsync(DateOnly today, CancellationToken ct = default);
}

/// <summary>
/// Напоминания по годовому плану актуализации (PLN-04).
///
/// Три повода, как в ТЗ: ежемесячная сводка 1-го числа, критическое напоминание за
/// N дней с копией курирующему заместителю Председателя Правления и уведомление о
/// просрочке в день наступления срока, если работа так и не начата.
///
/// Копия куратору идёт только на критических поводах. Класть его в копию ежемесячной
/// сводки — верный способ добиться, чтобы он перестал читать эти письма вовсе.
/// </summary>
public class PlanReminderService : IPlanReminderService
{
    private const string Entity = nameof(ActualizationPlanItem);

    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<PlanReminderService> _logger;

    public PlanReminderService(
        DelosferaDbContext db,
        INotificationService notifications,
        ILogger<PlanReminderService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> SendAsync(DateOnly today, CancellationToken ct = default)
    {
        var settings = await _db.ActualizationSettings.FirstOrDefaultAsync(ct) ?? new ActualizationSettings();

        var items = await _db.ActualizationPlanItems
            .Include(i => i.ResponsibleUnit)
            .Include(i => i.Curator)
            .Include(i => i.VndDocument)
            .Where(i => i.Status == PlanItemStatus.Planned || i.Status == PlanItemStatus.OnActualization)
            .ToListAsync(ct);

        if (items.Count == 0) return 0;

        var sent = 0;

        // 1-го числа — сводка ответственным подразделениям.
        if (settings.MonthlyDigestEnabled && today.Day == 1)
            sent += await SendMonthlyDigestAsync(items, today, ct);

        // Метки "уже отправлено" по каждой позиции — см. CriticalReminderKind/OverdueReminderKind
        // ниже. Без них сравнение
        // "daysLeft == порог"/"daysLeft == 0" ниже теряло бы напоминание навсегда, если ровно
        // в нужный день воркер не отработал (простой/деплой): на следующий день daysLeft уже
        // не равен порогу, и условие больше никогда не сработает для этого цикла.
        var itemIds = items.Select(i => i.Id).ToList();
        var sentKinds = await _db.Set<PlanItemEvent>()
            .Where(e => itemIds.Contains(e.PlanItemId)
                        && (e.Kind.StartsWith(CriticalReminderKindPrefix)
                            || e.Kind.StartsWith(OverdueReminderKindPrefix)))
            .Select(e => new {e.PlanItemId, e.Kind})
            .ToListAsync(ct);

        var sentKindsByItem = sentKinds
            .GroupBy(x => x.PlanItemId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Kind).ToHashSet());

        foreach (var item in items)
        {
            var daysLeft = item.DueDate.DayNumber - today.DayNumber;
            var alreadySent = sentKindsByItem.TryGetValue(item.Id, out var kinds) ? kinds : [];

            // "Порог достигнут или пройден" (было: точное совпадение) — см. комментарий выше.
            if (daysLeft <= settings.CriticalReminderDays && daysLeft >= 0)
            {
                var kind = CriticalReminderKind(item.DueDate);
                if (!alreadySent.Contains(kind))
                {
                    if (await SendAsync(item,
                        $"Критический срок актуализации: {item.Title}",
                        $"До плановой даты актуализации «{item.Title}» осталось {daysLeft} дн. " +
                        $"(срок {item.DueDate:dd.MM.yyyy}). Ответственное подразделение: " +
                        $"{item.ResponsibleUnit?.TitleRu ?? "не назначено"}.",
                        NotificationSeverity.Warning, includeCurator: true, ct))
                    {
                        sent++;
                        LogReminderSent(item.Id, kind);
                    }
                }

                continue;
            }

            // Наступила или прошла просрочка — но только если работа не начата: если цикл
            // актуализации уже идёт, писать «вы ничего не сделали» неверно.
            if (daysLeft <= 0 && item.Status == PlanItemStatus.Planned)
            {
                var kind = OverdueReminderKind(item.DueDate);
                if (!alreadySent.Contains(kind))
                {
                    if (await SendAsync(item,
                        $"Наступил срок актуализации: {item.Title}",
                        $"Сегодня наступила плановая дата актуализации «{item.Title}», " +
                        "но работа по документу не начата.",
                        NotificationSeverity.Urgent, includeCurator: true, ct))
                    {
                        sent++;
                        LogReminderSent(item.Id, kind);
                    }
                }
            }
        }

        if (sent > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Напоминаний по плану актуализации разослано: {Count}", sent);
        }

        return sent;
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    // Метки в PlanItemEvent.Kind (не текст для пользователя — см. Description там же), по
    // которым SendAsync выше проверяет "уже отправлено для этого срока актуализации". Срок
    // входит в метку, чтобы новый цикл (с новым DueDate) не считался уже уведомлённым.
    private const string CriticalReminderKindPrefix = "CriticalReminderSent:";
    private const string OverdueReminderKindPrefix = "OverdueReminderSent:";

    private static string CriticalReminderKind(DateOnly dueDate) =>
        $"{CriticalReminderKindPrefix}{dueDate:yyyy-MM-dd}";

    private static string OverdueReminderKind(DateOnly dueDate) =>
        $"{OverdueReminderKindPrefix}{dueDate:yyyy-MM-dd}";

    private void LogReminderSent(int itemId, string kind)
    {
        _db.Set<PlanItemEvent>().Add(new PlanItemEvent
        {
            PlanItemId = itemId,
            Kind = kind,
            Description = kind.StartsWith(CriticalReminderKindPrefix)
                ? "Отправлено критическое напоминание о приближении срока актуализации"
                : "Отправлено уведомление о наступлении срока актуализации",
            UserId = null,
            At = DateTime.UtcNow,
        });
    }

    private async Task<int> SendMonthlyDigestAsync(
        List<ActualizationPlanItem> items, DateOnly today, CancellationToken ct)
    {
        var sent = 0;

        foreach (var group in items.GroupBy(i => i.ResponsibleUnitId))
        {
            var recipients = await RecipientsAsync(group.Key, null, ct);
            if (recipients.Count == 0) continue;

            var upcoming = group
                .Where(i => i.DueDate >= today)
                .OrderBy(i => i.DueDate)
                .Take(20)
                .Select(i => $"• {i.Title} — до {i.DueDate:dd.MM.yyyy} " +
                             $"({i.DueDate.DayNumber - today.DayNumber} дн.)")
                .ToList();

            var overdue = group
                .Where(i => i.DueDate < today)
                .OrderBy(i => i.DueDate)
                .Select(i => $"• {i.Title} — просрочено с {i.DueDate:dd.MM.yyyy} " +
                             $"({today.DayNumber - i.DueDate.DayNumber} дн.)")
                .ToList();

            if (upcoming.Count == 0 && overdue.Count == 0) continue;

            var body = $"План актуализации ВНД на {today:MM.yyyy}.\n\n" +
                       (upcoming.Count > 0 ? "Приближаются сроки:\n" + string.Join("\n", upcoming) + "\n\n" : string.Empty) +
                       (overdue.Count > 0 ? "Просрочено:\n" + string.Join("\n", overdue) : "Просроченных позиций нет.");

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = "План актуализации ВНД: ежемесячная сводка",
                BodyRu = body,
                Category = NotificationCategory.Vnd,
                Severity = overdue.Count > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                EntityType = nameof(ActualizationPlan),
                EntityId = group.First().PlanId,
                Url = "/actualization/plan",
                UserIds = recipients,
            }, null);

            sent++;
        }

        return sent;
    }

    /// <summary>
    /// Отправляет напоминание. Возвращает false, если адресатов не нашлось: считать
    /// такую попытку отправленным письмом — значит писать в журнал неправду.
    /// </summary>
    private async Task<bool> SendAsync(
        ActualizationPlanItem item, string title, string body,
        NotificationSeverity severity, bool includeCurator, CancellationToken ct)
    {
        var recipients = await RecipientsAsync(
            item.ResponsibleUnitId, includeCurator ? item.CuratorUserId : null, ct);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Позиция плана {ItemId} «{Title}»: некому направить напоминание — " +
                "не назначены ни подразделение, ни куратор", item.Id, item.Title);
            return false;
        }

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = title,
            BodyRu = body,
            Category = NotificationCategory.Vnd,
            Severity = severity,
            EntityType = Entity,
            EntityId = item.Id,
            Url = "/actualization/plan",
            UserIds = recipients,
        }, null);

        return true;
    }

    /// <summary>
    /// Адресаты: руководитель ответственного подразделения и, на критических поводах,
    /// курирующий заместитель Председателя Правления.
    /// </summary>
    private async Task<List<int>> RecipientsAsync(int? unitId, int? curatorUserId, CancellationToken ct)
    {
        var recipients = new HashSet<int>();

        if (unitId is { } id)
        {
            var unit = await _db.OrganizationUnits
                .Where(u => u.Id == id)
                .Select(u => new {u.HeadUserId, u.CuratorUserId})
                .FirstOrDefaultAsync(ct);

            if (unit?.HeadUserId is { } head) recipients.Add(head);
            if (curatorUserId is null && unit?.CuratorUserId is { } unitCurator) recipients.Add(unitCurator);
        }

        if (curatorUserId is { } curator) recipients.Add(curator);

        return recipients.ToList();
    }
}
