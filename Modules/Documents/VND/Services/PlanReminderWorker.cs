using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Рассылка напоминаний по плану актуализации (PLN-04) — в 9:00 по времени банка.
///
/// Повторный запуск в тот же день на ОДНОМ и том же процессе безопасен — отметка о дне
/// последней рассылки хранится в процессе, а сама рассылка сверяется с датой.
///
/// При НЕСКОЛЬКИХ репликах приложения (несколько подов/контейнеров) этой защиты
/// недостаточно: у каждой реплики своя память, поэтому все они одновременно решают, что
/// сегодня ещё не отправляли, и рассылают свою копию. Advisory lock Postgres (см.
/// TryAcquireLockAsync) сериализует тик воркера между репликами — если он совпал по времени (как обычно и бывает,
/// когда реплики подняты одновременно), рассылку реально выполнит только одна из них, а
/// остальные увидят "не отправлено" уже после того, как первая реплика закоммитила решение —
/// см. отметки "уже отправлено" в самих PlanReminderService/ActualizationNotificationService.
/// Полной гарантии на случай реплик с сильно разъехавшимися по времени тиками это не даёт (для
/// этого нужна отдельная таблица с датой последнего запуска и миграция), но закрывает основной,
/// практически всегда актуальный случай — без миграции и без новой таблицы.
/// </summary>
public class PlanReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    // Ключ advisory lock'а Postgres — произвольное, но уникальное для этого воркера число
    // (не пересекается с ActualizationNotificationWorker, см. его ключ).
    private const long AdvisoryLockKey = 727_001;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlanReminderWorker> _logger;

    private DateOnly? _lastRunOn;

    public PlanReminderWorker(IServiceScopeFactory scopeFactory, ILogger<PlanReminderWorker> logger)
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

                if (_lastRunOn != today && TimeOnly.FromDateTime(clock.Now) >= SendAt)
                {
                    var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

                    // Пытаемся захватить advisory lock — если другая реплика уже держит его
                    // (обрабатывает этот же тик прямо сейчас), просто пропускаем тик: наша
                    // очередь — через Interval.
                    var gotLock = await TryAcquireLockAsync(db, stoppingToken);
                    if (gotLock)
                    {
                        try
                        {
                            var reminders = scope.ServiceProvider.GetRequiredService<IPlanReminderService>();
                            await reminders.SendAsync(today, stoppingToken);
                        }
                        finally
                        {
                            await ReleaseLockAsync(db, CancellationToken.None);
                        }
                    }

                    // Отмечаем день как обработанный ДАЖЕ если лок не достался (gotLock == false):
                    // значит сегодня уже занимается другая реплика, и нам сегодня повторно
                    // пытаться не нужно — иначе ровно эта реплика попытается снова через Interval
                    // и, скорее всего, всё же захватит лок (к тому моменту первая уже закончит и
                    // отпустит его) и отправит дубликат. Если единственная реплика, забравшая
                    // лок, упадёт до отправки — рассылка за сегодня не уйдёт вообще, но при
                    // перезапуске процесса _lastRunOn обнулится и попытка повторится.
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlanReminderWorker: ошибка рассылки напоминаний по плану актуализации");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// pg_try_advisory_lock — session-level lock: держится, пока держим это же соединение
    /// открытым явно (OpenConnectionAsync/CloseConnectionAsync — тот самый поддерживаемый EF
    /// Core способ занять соединение под операцию, которая должна пережить несколько команд).
    /// Никакой миграции/новой таблицы не требуется — сам механизм блокировок целиком в Postgres.
    /// </summary>
    private static async Task<bool> TryAcquireLockAsync(DelosferaDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);

        var results = await db.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", AdvisoryLockKey)
            .ToListAsync(ct);

        var acquired = results.Count > 0 && results[0];
        if (!acquired)
            await db.Database.CloseConnectionAsync(); // не держим соединение занятым зря

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
