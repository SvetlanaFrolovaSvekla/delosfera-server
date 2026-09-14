using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Analytics.Services;

/// <summary>
/// Утренняя рассылка дайджеста (УВ-15) — в 8:00 по времени банка, раньше напоминаний о
/// сроках (9:00): сводка за день должна лечь на стол до того, как начнут гореть сроки.
/// Один прогон в день; та же схема защиты от повторов, что и у SzDeadlineWorker.
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
}
