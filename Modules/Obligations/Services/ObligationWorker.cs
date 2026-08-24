namespace delosfera_server.Modules.Obligations.Services;

/// <summary>
/// Ведёт календарь обязательств: заводит периоды вперёд, закрывает те, что
/// подтверждены заседаниями, и помечает просроченные.
///
/// Работает раз в шесть часов, а не раз в сутки: заседание, заведённое утром,
/// должно закрыть месячное обязательство в тот же день. Иначе секретарь видит
/// «не проведено» после проведённого заседания и заводит второе.
/// </summary>
public class ObligationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ObligationWorker> _logger;

    public ObligationWorker(IServiceScopeFactory scopes, ILogger<ObligationWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IObligationService>();

                var (created, fulfilled, missed) = await service.SyncAsync(stoppingToken);

                if (created > 0 || fulfilled > 0 || missed > 0)
                {
                    _logger.LogInformation(
                        "Обязательства: заведено периодов {Created}, закрыто заседаниями {Fulfilled}, просрочено {Missed}",
                        created, fulfilled, missed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось обновить календарь обязательств");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
