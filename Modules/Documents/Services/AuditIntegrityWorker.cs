namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Сопровождение хеш-цепи аудита (AUD-1).
///
/// На старте однократно достраивает цепь по легаси-записям без хеша (после первого
/// развёртывания AUD-1), затем раз в сутки проверяет целостность и при разрыве кричит в
/// лог как об инциденте безопасности — разрыв означает ретроактивное изменение журнала.
/// </summary>
public class AuditIntegrityWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditIntegrityWorker> _logger;

    public AuditIntegrityWorker(IServiceScopeFactory scopes, ILogger<AuditIntegrityWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        // Разовый бэкфилл легаси-цепи.
        try
        {
            using var scope = _scopes.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var filled = await audit.BackfillChainAsync(stoppingToken);
            if (filled > 0)
                _logger.LogInformation("AUD-1: хеш-цепь аудита достроена по {Count} легаси-записям", filled);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AUD-1: не удалось достроить хеш-цепь аудита");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var status = await audit.VerifyChainAsync(stoppingToken);
                if (status.Valid)
                    _logger.LogInformation("AUD-1: цепь аудита цела, проверено записей: {Count}", status.CheckedCount);
                else
                    _logger.LogError(
                        "AUD-1: НАРУШЕНА целостность аудита у записи {Id}: {Reason}. Возможна ретроактивная правка журнала.",
                        status.BrokenAtId, status.Reason);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AUD-1: проверка целостности аудита не удалась");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
