using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Correspondence.Services;

/// <summary>
/// Напоминания о сроках исполнения корреспонденции (КР-1) — в 9:00 по времени банка.
///
/// Утро выбрано так же, как в напоминаниях по СЗ: письмо о завтрашнем сроке, пришедшее
/// вечером, читают на следующий день, когда срок уже наступил.
/// </summary>
public class LetterDeadlineWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LetterDeadlineWorker> _logger;

    private DateOnly? _lastRunOn;

    public LetterDeadlineWorker(IServiceScopeFactory scopeFactory, ILogger<LetterDeadlineWorker> logger)
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
                    var notifier = scope.ServiceProvider.GetRequiredService<ILetterDeadlineNotifier>();
                    await notifier.SendAsync(today, stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LetterDeadlineWorker: ошибка рассылки напоминаний по срокам корреспонденции");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
