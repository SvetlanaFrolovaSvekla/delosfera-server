using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Substitutions.Services;

/// <summary>
/// Раз в сутки в 9:00 по времени банка напоминает о «зависших» заявках на замещение
/// (ЗМ-SLA) — по образцу SzDeadlineWorker. Утро выбрано, чтобы напоминание попало в
/// начало рабочего дня согласующего, а не осело в ящике вечером.
/// </summary>
public class SubstitutionReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubstitutionReminderWorker> _logger;

    private DateOnly? _lastRunOn;

    public SubstitutionReminderWorker(IServiceScopeFactory scopeFactory, ILogger<SubstitutionReminderWorker> logger)
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
                    var notifier = scope.ServiceProvider.GetRequiredService<ISubstitutionReminderNotifier>();
                    await notifier.SendAsync(stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubstitutionReminderWorker: ошибка рассылки напоминаний по заявкам на замещение");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
