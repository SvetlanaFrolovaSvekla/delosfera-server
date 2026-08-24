using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Obligations.Models;

namespace delosfera_server.Modules.Obligations.Services;

public interface IObligationService
{
    /// <summary>
    /// Заводит недостающие периоды и обновляет их состояния. Возвращает, сколько
    /// периодов создано, сколько закрыто автоматически и сколько просрочено.
    /// </summary>
    Task<(int Created, int AutoFulfilled, int Missed)> SyncAsync(CancellationToken ct = default);

    Task FulfilAsync(int periodId, string? comment, int currentUserId, CancellationToken ct = default);
    Task WaiveAsync(int periodId, string reason, int currentUserId, CancellationToken ct = default);
}

/// <summary>
/// Ведёт календарь регулярных обязательств.
///
/// Главное здесь — автоматическое закрытие периодов по заседаниям. Обязательство
/// «комитет по рискам заседает ежемесячно» не должен закрывать человек отметкой:
/// заседание либо заведено в системе, либо нет, и вторая запись об этом же факте
/// только создаёт расхождение между повесткой и календарём.
/// </summary>
public class ObligationService : IObligationService
{
    /// <summary>
    /// На сколько периодов вперёд заводим записи. Один вперёд нужен, чтобы
    /// ответственный видел ближайший срок, а не узнавал о нём в день наступления.
    /// </summary>
    private const int PeriodsAhead = 1;

    private readonly DelosferaDbContext _db;

    public ObligationService(DelosferaDbContext db) => _db = db;

    public async Task<(int Created, int AutoFulfilled, int Missed)> SyncAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var obligations = await _db.RecurringObligations
            .Where(o => o.IsActive)
            .ToListAsync(ct);

        var created = 0;

        foreach (var obligation in obligations)
        {
            // Горизонт: текущий период плюс заданное число вперёд.
            var (_, currentEnd) = ObligationCalendar.PeriodOf(today, obligation.Periodicity);
            var horizon = currentEnd;
            for (var i = 0; i < PeriodsAhead; i++)
            {
                var next = ObligationCalendar.NextStart(horizon);
                (_, horizon) = ObligationCalendar.PeriodOf(next, obligation.Periodicity);
            }

            var existing = await _db.ObligationPeriods
                .Where(p => p.ObligationId == obligation.Id)
                .Select(p => p.PeriodStart)
                .ToListAsync(ct);

            var known = existing.ToHashSet();

            foreach (var (start, end, due) in ObligationCalendar.Periods(obligation, horizon))
            {
                if (known.Contains(start)) continue;

                _db.ObligationPeriods.Add(new ObligationPeriod
                {
                    ObligationId = obligation.Id,
                    PeriodStart = start,
                    PeriodEnd = end,
                    DueDate = due,
                    Status = ObligationPeriodStatus.Pending,
                    CreatedAt = now,
                    UpdatedAt = now,
                });

                created++;
            }
        }

        if (created > 0)
            await _db.SaveChangesAsync(ct);

        var autoFulfilled = await AutoFulfilMeetingsAsync(ct);
        var missed = await MarkMissedAsync(today, ct);

        return (created, autoFulfilled, missed);
    }

    /// <summary>
    /// Закрывает периоды обязательств вида «заседание проведено» по фактическим
    /// заседаниям органа. Берётся первое заседание в периоде — им обязательство и
    /// исполнено; остальные к нему уже ничего не добавляют.
    /// </summary>
    private async Task<int> AutoFulfilMeetingsAsync(CancellationToken ct)
    {
        var pending = await _db.ObligationPeriods
            .Include(p => p.Obligation)
            .Where(p => p.Status == ObligationPeriodStatus.Pending
                        && p.Obligation!.Kind == ObligationKind.MeetingHeld
                        && p.Obligation.Body != null)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var closed = 0;

        foreach (var period in pending)
        {
            var body = period.Obligation!.Body!.Value;

            var meeting = await _db.Meetings
                .Where(m => m.Body == body
                            && m.Date >= period.PeriodStart
                            && m.Date <= period.PeriodEnd)
                .OrderBy(m => m.Date)
                .Select(m => new {m.Id, m.Date})
                .FirstOrDefaultAsync(ct);

            if (meeting is null) continue;

            period.Status = ObligationPeriodStatus.Fulfilled;
            period.MeetingId = meeting.Id;
            // Момент исполнения — день заседания, а не день, когда сработала служба.
            // Иначе заседание, заведённое задним числом, выглядело бы просроченным.
            period.FulfilledAt = meeting.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            period.UpdatedAt = DateTime.UtcNow;

            closed++;
        }

        if (closed > 0)
            await _db.SaveChangesAsync(ct);

        return closed;
    }

    private async Task<int> MarkMissedAsync(DateOnly today, CancellationToken ct) =>
        await _db.ObligationPeriods
            .Where(p => p.Status == ObligationPeriodStatus.Pending && p.DueDate < today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, ObligationPeriodStatus.Missed)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

    public async Task FulfilAsync(int periodId, string? comment, int currentUserId, CancellationToken ct = default)
    {
        var period = await Load(periodId, ct);

        if (period.Obligation?.Kind == ObligationKind.MeetingHeld)
            throw new InvalidOperationException(
                "Обязательство закрывается заседанием — заведите заседание органа за этот период.");

        period.Status = ObligationPeriodStatus.Fulfilled;
        period.FulfilledAt = DateTime.UtcNow;
        period.FulfilledByUserId = currentUserId;
        period.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        period.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Снять период: в этом промежутке обязательство не требовалось. Причина
    /// обязательна — снятый без объяснения период неотличим от забытого.
    /// </summary>
    public async Task WaiveAsync(int periodId, string reason, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите, почему период снят.");

        var period = await Load(periodId, ct);

        period.Status = ObligationPeriodStatus.Waived;
        period.Comment = reason.Trim();
        period.FulfilledByUserId = currentUserId;
        period.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<ObligationPeriod> Load(int periodId, CancellationToken ct) =>
        await _db.ObligationPeriods
            .Include(p => p.Obligation)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct)
        ?? throw new KeyNotFoundException("Период не найден.");
}
