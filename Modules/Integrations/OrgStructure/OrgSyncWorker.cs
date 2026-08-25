using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Integrations.OrgStructure;

/// <summary>
/// Забирает оргструктуру из портала по расписанию.
///
/// Интервал берётся из настроек и перечитывается каждый круг: администратор
/// поменял его в интерфейсе — новый срок действует со следующего раза,
/// без перезапуска службы.
///
/// Портал просит не опрашивать его в цикле, поэтому по умолчанию раз в сутки,
/// а короче пяти минут интервал не ставится вовсе.
/// </summary>
public class OrgSyncWorker(IServiceProvider services, ILogger<OrgSyncWorker> log)
    : BackgroundService
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdlePause = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Первый круг — не сразу: при старте система занята миграциями и
        // прогревом, а обход портала страницами это заметно замедлит.
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            TimeSpan pause;

            try
            {
                pause = await TickAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                // Служба не должна умирать от одной неудачи: портал полежит
                // и поднимется, а без неё синхронизация не возобновится до
                // перезапуска приложения.
                log.LogError(e, "Круг синхронизации оргструктуры прервался");
                pause = IdlePause;
            }

            await Task.Delay(pause, ct);
        }
    }

    /// <summary>Один круг. Возвращает, сколько ждать до следующего.</summary>
    private async Task<TimeSpan> TickAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

        var settings = await db.OrgStructureSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.Enabled)
            return IdlePause;

        var interval = TimeSpan.FromMinutes(Math.Max(settings.SyncIntervalMinutes, MinimumInterval.TotalMinutes));

        var lastRun = await db.OrgSyncRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (lastRun is not null)
        {
            var due = lastRun.StartedAt + interval;
            if (due > DateTime.UtcNow)
                return due - DateTime.UtcNow;
        }

        var sync = scope.ServiceProvider.GetRequiredService<IOrgSyncService>();
        var run = await sync.RunAsync(startedByUserId: null, ct);

        log.LogInformation(
            "Оргструктура: {Outcome}. Подразделений получено {Units}, заведено {Created}; " +
            "сотрудников сопоставлено {Matched}, не найдено {Unmatched}.",
            run.Outcome, run.UnitsReceived, run.UnitsCreated, run.EmployeesMatched, run.EmployeesUnmatched);

        return interval;
    }
}
