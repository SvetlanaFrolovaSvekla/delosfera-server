using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionReminderNotifier
{
    /// <summary>Разослать напоминания по «зависшим» этапам согласования замещений. Возвращает число уведомлений.</summary>
    Task<int> SendAsync(CancellationToken ct = default);
}

/// <summary>
/// Напоминания о «зависших» заявках на замещение (ЗМ-SLA).
///
/// В отличие от служебных записок, у замещений не было ни срока, ни напоминаний: заявка
/// на согласовании могла стоять неделями, и узнавали об этом, когда замещать уже поздно.
/// Здесь для текущего этапа маршрута считается, сколько рабочих дней он открыт (от
/// ActivatedAt), и при превышении норматива (SubstitutionSlaSettings) текущему согласующему
/// — и инициатору — уходит напоминание. Считаем рабочие дни без учёта праздников: рабочий
/// календарь с праздниками — отдельная задача (COND-1), здесь исключаем только выходные.
/// </summary>
public class SubstitutionReminderNotifier : ISubstitutionReminderNotifier
{
    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IBankClock _clock;
    private readonly ISubstitutionService _substitutions;
    private readonly ILogger<SubstitutionReminderNotifier> _logger;

    private const string Entity = "SubstitutionRequest";

    public SubstitutionReminderNotifier(
        DelosferaDbContext db, INotificationService notifications, IBankClock clock,
        ISubstitutionService substitutions, ILogger<SubstitutionReminderNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        _clock = clock;
        _substitutions = substitutions;
        _logger = logger;
    }

    public async Task<int> SendAsync(CancellationToken ct = default)
    {
        var slaDays = await _substitutions.GetSlaDaysAsync(ct);
        var today = _clock.Today;

        // Заявки на согласовании с их этапами и назначенными согласующими.
        var requests = await _db.SubstitutionRequests
            .AsNoTracking()
            .Where(r => r.Status == SubstitutionStatus.OnApproval)
            .Select(r => new
            {
                r.Id,
                r.RegNumber,
                r.Subject,
                r.InitiatorUserId,
                Active = r.Approvals
                    .Where(a => a.State == SubstitutionApprovalState.Active)
                    .Select(a => new { a.UserId, a.RoleLabel, a.ActivatedAt })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var sent = 0;

        foreach (var r in requests)
        {
            if (r.Active?.ActivatedAt is not { } activatedAtUtc) continue;

            var activatedOn = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(activatedAtUtc, _clock.Zone));

            var elapsed = BusinessDaysBetween(activatedOn, today);
            if (elapsed <= slaDays) continue;

            var overdueBy = elapsed - slaDays;
            var number = string.IsNullOrWhiteSpace(r.RegNumber) ? $"#{r.Id}" : r.RegNumber;

            // Текущий согласующий — главный адресат; инициатор в копии, чтобы видел, что заявка стоит.
            var recipients = new HashSet<int> { r.Active.UserId, r.InitiatorUserId };

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = "Заявка на замещение ждёт согласования",
                BodyRu = $"Заявка на замещение {number} на этапе «{r.Active.RoleLabel}» " +
                         $"без решения {elapsed} раб. дн. — превышен норматив {slaDays} дн. " +
                         $"(просрочка {overdueBy} дн.). Тема: {r.Subject}",
                Category = NotificationCategory.Other,
                Severity = NotificationSeverity.Urgent,
                EntityType = Entity,
                EntityId = r.Id,
                Url = $"/substitutions/{r.Id}",
                UserIds = recipients.ToList(),
            }, null);

            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Напоминаний по зависшим заявкам на замещение разослано: {Count}", sent);

        return sent;
    }

    /// <summary>Число рабочих дней (пн–пт) строго после <paramref name="from"/> и по <paramref name="to"/> включительно.</summary>
    private static int BusinessDaysBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;

        var days = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                days++;

        return days;
    }
}
