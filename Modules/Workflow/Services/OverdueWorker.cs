namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Фоновый воркер: периодически применяет автоакцепт/эскалацию к просроченным
/// участникам маршрутов (TID-08/12). Интервал — по нормативам, здесь опрос раз в минуту.
/// </summary>
public class OverdueWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueWorker> _logger;

    public OverdueWorker(IServiceScopeFactory scopeFactory, ILogger<OverdueWorker> logger)
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
                var engine = scope.ServiceProvider.GetRequiredService<IRouteEngine>();
                await engine.ApplyOverdueAsync(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OverdueWorker: ошибка обработки просроченных участников");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
