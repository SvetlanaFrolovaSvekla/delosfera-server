namespace delosfera_server.Modules.Vnd.Services;

/// <summary>Раз в N минут проверяет просроченные этапы согласования и выдержку,
/// автоматически их закрывает (просрочка = согласовано)</summary>
public class VndApprovalTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VndApprovalTimeoutBackgroundService> _logger;

    public VndApprovalTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<VndApprovalTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var approvalService = scope.ServiceProvider.GetRequiredService<IVndApprovalService>();
                await approvalService.ProcessTimeoutsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке просроченных согласований ВНД");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}