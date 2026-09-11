using delosfera_server.Common.Services;

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
/// Повторный запуск в тот же день безопасен — оба метода сервиса сами проверяют свои условия;
/// отметки последнего запуска здесь только чтобы не дёргать сервис зря каждые 15 минут после
/// 9:00.
/// </summary>
public class ActualizationNotificationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

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

                if (TimeOnly.FromDateTime(clock.Now) >= SendAt)
                {
                    var notifications = scope.ServiceProvider.GetRequiredService<IActualizationNotificationService>();

                    if (_lastMonthlyDigestRunOn != today)
                    {
                        await notifications.SendMonthlyDigestAsync(today, stoppingToken);
                        _lastMonthlyDigestRunOn = today;
                    }

                    if (_lastCriticalRemindersRunOn != today)
                    {
                        await notifications.SendCriticalRemindersAsync(today, stoppingToken);
                        _lastCriticalRemindersRunOn = today;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ActualizationNotificationWorker: ошибка рассылки по актуализации ВНД");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
