using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Рассылки по актуализации ВНД (раздел "Уведомления" → "Настройки рассылок" →
/// "Нормотворчество") — в 9:00 по времени банка, тот же ритм, что PlanReminderWorker у старого
/// плана актуализации (PLN-04):
///
/// 1. Ежемесячная сводка — только 1-го числа (SendMonthlyDigestAsync сама ничего не делает
///    в другие дни).
/// 2. Критические напоминания — каждый день, по порогам из настроек (SendCriticalRemindersAsync
///    сама ничего не делает, если рассылка выключена или пороги не заданы).
///
/// Повторный запуск в тот же день на ОДНОМ процессе безопасен — отметки последнего запуска
/// здесь только чтобы не дёргать сервис зря каждые 15 минут после 9:00. При НЕСКОЛЬКИХ репликах
/// этой in-memory защиты недостаточно — см. подробный комментарий у PlanReminderWorker.
/// AdvisoryLockKey сериализует тик между репликами тем же способом (pg_try_advisory_lock,
/// без миграций), закрывая практический случай (реплики подняты одновременно).
/// </summary>
public class ActualizationNotificationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    // Отдельный ключ от PlanReminderWorker.AdvisoryLockKey (727_001) — иначе воркеры блокировали
    // бы друг друга без необходимости.
    private const long AdvisoryLockKey = 727_002;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActualizationNotificationWorker> _logger;

    private DateOnly? _lastMonthlyDigestRunOn;
    private DateOnly? _lastCriticalRemindersRunOn;

    public ActualizationNotificationWorker(
        IServiceScopeFactory scopeFactory, ILogger<ActualizationNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var clock = scope.ServiceProvider.GetRequiredService<IBankClock>();
                var today = clock.Today;

                if (TimeOnly.FromDateTime(clock.Now) >= SendAt
                    && (_lastMonthlyDigestRunOn != today || _lastCriticalRemindersRunOn != today))
                {
                    var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

                    var gotLock = await TryAcquireLockAsync(db, stoppingToken);
                    if (gotLock)
                    {
                        try
                        {
                            var notifications =
                                scope.ServiceProvider.GetRequiredService<IActualizationNotificationService>();

                            if (_lastMonthlyDigestRunOn != today)
                                await notifications.SendMonthlyDigestAsync(today, stoppingToken);

                            if (_lastCriticalRemindersRunOn != today)
                                await notifications.SendCriticalRemindersAsync(today, stoppingToken);
                        }
                        finally
                        {
                            await ReleaseLockAsync(db, CancellationToken.None);
                        }
                    }

                    // Отмечаем день обработанным даже без лока — см. подробное объяснение этого
                    // компромисса у одноимённой строки в PlanReminderWorker.
                    _lastMonthlyDigestRunOn = today;
                    _lastCriticalRemindersRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ActualizationNotificationWorker: ошибка рассылки по актуализации ВНД");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>См. подробный комментарий у одноимённого метода в PlanReminderWorker.</summary>
    private static async Task<bool> TryAcquireLockAsync(DelosferaDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);

        var results = await db.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", AdvisoryLockKey)
            .ToListAsync(ct);

        var acquired = results.Count > 0 && results[0];
        if (!acquired)
            await db.Database.CloseConnectionAsync();

        return acquired;
    }

    private static async Task ReleaseLockAsync(DelosferaDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", ct);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
