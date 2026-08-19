using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Рассылка напоминаний по плану актуализации (PLN-04) — в 9:00 по времени банка.
///
/// Повторный запуск в тот же день безопасен: отметка о дне последней рассылки
/// хранится в процессе, а сама рассылка сверяется с датой — сводка уходит только
/// 1-го числа, критические напоминания только в свой день.
/// </summary>
public class PlanReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

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
                    var reminders = scope.ServiceProvider.GetRequiredService<IPlanReminderService>();
                    await reminders.SendAsync(today, stoppingToken);
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
}
