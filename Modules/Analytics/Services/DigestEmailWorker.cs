using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;

namespace delosfera_server.Modules.Analytics.Services;

/// <summary>
/// Утренняя рассылка дайджеста (УВ-15) — в 8:00 по времени банка, раньше напоминаний о
/// сроках (9:00): сводка за день должна лечь на стол до того, как начнут гореть сроки.
/// Один прогон в день; та же схема защиты от повторов, что и у SzDeadlineWorker.
///
/// _lastRunOn живёт только в памяти процесса и сбрасывается на null при каждом перезапуске
/// (в т.ч. при обычной выкладке новой сборки бэкенда) — без GetLastRunOnFromHistoryAsync ниже
/// это означало бы, что перезапуск ПОСЛЕ 8:00 отправляет дайджест ещё раз в тот же день, даже
/// если утром он уже ушёл. GetLastRunOnFromHistoryAsync восстанавливает "уже отправляли
/// сегодня" из фактической истории писем (OutgoingEmail), которая переживает перезапуск, — и
/// делает это один раз при старте воркера, дальше работает обычная проверка по _lastRunOn.
/// </summary>
public class DigestEmailWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(8, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestEmailWorker> _logger;

    private DateOnly? _lastRunOn;

    public DigestEmailWorker(IServiceScopeFactory scopeFactory, ILogger<DigestEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _lastRunOn = await GetLastRunOnFromHistoryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var clock = scope.ServiceProvider.GetRequiredService<IBankClock>();
                var today = clock.Today;

                if (_lastRunOn != today && TimeOnly.FromDateTime(clock.Now) >= SendAt)
                {
                    var service = scope.ServiceProvider.GetRequiredService<IDigestEmailService>();
                    await service.SendDailyAsync(stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DigestEmailWorker: ошибка утренней рассылки дайджеста");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>Сегодняшний дайджест уже отправляли (в т.ч. до перезапуска процесса) —
    /// определяем не по in-memory полю (оно после перезапуска пустое), а по тому, есть ли уже в
    /// очереди писем (OutgoingEmail) хоть одно письмо дайджеста, поставленное сегодня по времени
    /// банка. IBankClock.Zone — как раз для такого пересчёта локальной границы дня в UTC (см.
    /// комментарий на интерфейсе). Ошибка проверки (например, БД временно недоступна при
    /// старте) не должна блокировать воркер насовсем — тогда считаем, что сегодня ещё не
    /// отправляли, и полагаемся на обычную защиту от повторов внутри одного процесса.</summary>
    private async Task<DateOnly?> GetLastRunOnFromHistoryAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IBankClock>();
            var today = clock.Today;

            var todayStartLocal = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, clock.Zone);

            var alreadySentToday = await db.OutgoingEmails
                .AnyAsync(e => e.Subject == DigestEmailMessages.Subject && e.CreatedAt >= todayStartUtc, ct);

            return alreadySentToday ? today : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DigestEmailWorker: не удалось проверить по истории писем, отправляли ли дайджест сегодня");
            return null;
        }
    }
}
