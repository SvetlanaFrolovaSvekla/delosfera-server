using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Напоминания о сроках исполнения поручений (SZ-03) — в 9:00 по времени банка.
///
/// Утро выбрано не случайно: письмо о завтрашнем сроке, пришедшее вечером, читают
/// на следующий день, когда срок уже наступил.
/// </summary>
public class SzDeadlineWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SzDeadlineWorker> _logger;

    private DateOnly? _lastRunOn;

    public SzDeadlineWorker(IServiceScopeFactory scopeFactory, ILogger<SzDeadlineWorker> logger)
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
                    var notifier = scope.ServiceProvider.GetRequiredService<ISzDeadlineNotifier>();
                    await notifier.SendAsync(today, stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SzDeadlineWorker: ошибка рассылки напоминаний по срокам поручений");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
