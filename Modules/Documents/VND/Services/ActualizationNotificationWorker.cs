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
///
/// Перезапуск процесса (например, обычная выкладка новой сборки бэкенда) обнуляет обе отметки —
/// они живут только в памяти. Для критических напоминаний это не страшно — SendCriticalRemindersAsync
/// сам не отправляет повторно то, что уже отправлял (пороги отмечаются в журнале активности, см.
/// LoadSentReminderMarkersAsync в ActualizationNotificationService). А вот ежемесячная сводка
/// такой защиты не имела: перезапуск 1-го числа после 9:00 рассылал её ещё раз —
/// GetLastMonthlyDigestRunOnFromHistoryAsync ниже восстанавливает эту отметку из фактической
/// истории уведомлений/писем при старте воркера, так же как в DigestEmailWorker/PlanReminderWorker.
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
        _lastMonthlyDigestRunOn = await GetLastMonthlyDigestRunOnFromHistoryAsync(stoppingToken);

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

    /// <summary>Ежемесячную сводку по актуализации ВНД сегодня уже отправляли (в т.ч. до
    /// перезапуска процесса) — определяем не по in-memory _lastMonthlyDigestRunOn (после
    /// перезапуска оно пустое), а по фактической истории: в зависимости от настроек канала
    /// (ActualizationNotificationSettings.MonthlyDigestNotifyInApp/Email — см.
    /// ActualizationNotificationService.SendMonthlyDigestAsync) сводка сегодня могла лечь либо
    /// в Notifications, либо сразу в очередь писем (OutgoingEmail) без записи в Notifications —
    /// проверяем оба места. Тема письма стабильна по префиксу (месяц/год и подразделение —
    /// переменная часть, см. ActualizationNotificationMessages.MonthlyDigestSubjectPrefix).
    /// Сводка возможна только 1-го числа — в другие дни сразу возвращаем null. Ошибка проверки
    /// не должна блокировать воркер насовсем — тогда считаем, что сегодня ещё не отправляли.</summary>
    private async Task<DateOnly?> GetLastMonthlyDigestRunOnFromHistoryAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IBankClock>();
            var today = clock.Today;

            if (today.Day != 1) return null;

            var todayStartLocal = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, clock.Zone);

            var alreadySentAsNotification = await db.Notifications
                .AnyAsync(n => n.EntityType == "OrganizationUnit"
                               && n.TitleRu.StartsWith(ActualizationNotificationMessages.MonthlyDigestSubjectPrefix)
                               && n.CreatedAt >= todayStartUtc, ct);

            var alreadySentAsEmail = !alreadySentAsNotification && await db.OutgoingEmails
                .AnyAsync(e => e.Subject.StartsWith(ActualizationNotificationMessages.MonthlyDigestSubjectPrefix)
                               && e.CreatedAt >= todayStartUtc, ct);

            return (alreadySentAsNotification || alreadySentAsEmail) ? today : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ActualizationNotificationWorker: не удалось проверить по истории, отправляли ли ежемесячную сводку сегодня");
            return null;
        }
    }
}
