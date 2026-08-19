using delosfera_server.Common.Services;

namespace delosfera_server.Modules.Meetings.Services;

/// <summary>
/// Рассылка напоминаний об исполнении протоколов КПА, Правления и комитетов.
///
/// ТЗ задаёт время рассылки — 9:00, и это время банка, а не UTC: в Бишкеке разница
/// шесть часов, и напоминание по UTC приходило бы ночью. Воркер просыпается раз в
/// четверть часа и запускает рассылку в первое окно после девяти утра; повторный
/// запуск в тот же день безопасен — отметка LastReminderOn не даёт письму уйти дважды.
/// </summary>
public class MeetingReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SendAt = new(9, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingReminderWorker> _logger;

    private DateOnly? _lastRunOn;

    public MeetingReminderWorker(IServiceScopeFactory scopeFactory, ILogger<MeetingReminderWorker> logger)
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

                var now = clock.Now;
                var today = clock.Today;

                if (_lastRunOn != today && TimeOnly.FromDateTime(now) >= SendAt)
                {
                    var notifications = scope.ServiceProvider
                        .GetRequiredService<IMeetingNotificationService>();

                    await notifications.SendDueRemindersAsync(today, stoppingToken);
                    _lastRunOn = today;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MeetingReminderWorker: ошибка рассылки напоминаний по протоколам");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
