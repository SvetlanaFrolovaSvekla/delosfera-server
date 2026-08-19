namespace delosfera_server.Modules.Integrations.Mail;

/// <summary>
/// Отправка накопившихся писем (INT-02). Раз в полминуты: уведомление о задаче
/// не срочнее этого, а relay банка не любит поток соединений.
/// </summary>
public class MailWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MailWorker> _logger;

    public MailWorker(IServiceScopeFactory scopeFactory, ILogger<MailWorker> logger)
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
                var queue = scope.ServiceProvider.GetRequiredService<IMailQueue>();

                if (queue.Enabled)
                {
                    var sent = await queue.FlushAsync(stoppingToken);
                    if (sent > 0) _logger.LogInformation("Отправлено писем: {Count}", sent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MailWorker: ошибка отправки очереди писем");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
