using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Рассылка ежемесячной сводки по актуализации ВНД (раздел "Уведомления" → "Настройки
/// рассылок" → "Нормотворчество") — в 9:00 по времени банка, тот же ритм, что
/// PlanReminderWorker у старого плана актуализации (PLN-04).
///
/// Повторный запуск в тот же день безопасен: SendMonthlyDigestAsync сам ничего не делает,
/// если сегодня не 1-е число или рассылка выключена в настройках — отметка последнего запуска
/// здесь только чтобы не дёргать сервис зря каждые 15 минут после 9:00.
/// </summary>
public class ActualizationNotificationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActualizationNotificationWorker> _logger;

    private DateOnly? _lastRunOn;

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

                if (_lastRunOn != today && TimeOnly.FromDateTime(clock.Now) >= SendAt)
                {
                    var notifications = scope.ServiceProvider.GetRequiredService<IActualizationNotificationService>();
                    await notifications.SendMonthlyDigestAsync(today, stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ActualizationNotificationWorker: ошибка рассылки ежемесячной сводки по актуализации ВНД");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
