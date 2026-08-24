using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Feedback.Services;

/// <summary>
/// Удаляет старые записи о посещениях.
///
/// Пятьсот сотрудников дают десятки тысяч переходов в день — за год это миллионы
/// строк ради вопросов, которые звучат «что открывали на прошлой неделе». Без чистки
/// таблица посещаемости становится самой большой в базе, и первым это заметит не
/// администратор, а резервное копирование.
///
/// Журнал действий эта чистка не трогает: там след документа, и он живёт по своим
/// правилам хранения.
/// </summary>
public class PageVisitCleanupWorker : BackgroundService
{
    /// <summary>
    /// Сколько дней держим. Полгода закрывают любой разумный вопрос об обкатке и
    /// о сезонности работы; за более старым идут в журнал действий.
    /// </summary>
    private const int DefaultRetentionDays = 180;

    /// <summary>
    /// Удаляем частями. Один запрос на миллион строк держит блокировку и место в
    /// журнале транзакций дольше, чем стоит любая уборка.
    /// </summary>
    private const int BatchSize = 20_000;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>Не при старте: первые минуты после запуска нужны тем, кто работает.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PageVisitCleanupWorker> _logger;

    public PageVisitCleanupWorker(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<PageVisitCleanupWorker> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Уборка — не то, ради чего стоит ронять приложение. Пишем и ждём сутки.
                _logger.LogError(ex, "Не удалось почистить журнал посещений");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        var days = _configuration.GetValue<int?>("Usage:RetentionDays") ?? DefaultRetentionDays;
        if (days <= 0)
        {
            _logger.LogInformation("Чистка журнала посещений отключена настройкой");
            return;
        }

        var threshold = DateTime.UtcNow.Date.AddDays(-days);

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

        var removedTotal = 0;

        while (!ct.IsCancellationRequested)
        {
            // ExecuteDelete с ограничением: удаляем по частям, а не всё сразу.
            var removed = await db.PageVisits
                .Where(v => v.VisitedAt < threshold)
                .OrderBy(v => v.Id)
                .Take(BatchSize)
                .ExecuteDeleteAsync(ct);

            removedTotal += removed;

            if (removed < BatchSize)
                break;
        }

        if (removedTotal > 0)
            _logger.LogInformation(
                "Журнал посещений: удалено {Count} записей старше {Days} дней",
                removedTotal, days);
    }
}
