using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.PowerOfAttorney.Models;

namespace delosfera_server.Modules.PowerOfAttorney.Services;

/// <summary>
/// Закрывает истёкшие доверенности и предупреждает о скором истечении.
///
/// Состояние хранится, а не вычисляется по датам, — и потому кто-то должен его
/// переводить. Иначе реестр показывал бы «действует» у доверенности, срок которой
/// вышел месяц назад, и на вопрос «вправе ли он подписать» отвечал бы неправдой.
///
/// Предупреждение важнее самого закрытия: доверенность, истёкшая незамеченной,
/// обнаруживается в момент, когда представитель уже стоит у нотариуса.
/// </summary>
public class PoaExpiryWorker : BackgroundService
{
    /// <summary>За сколько дней предупреждать. Две недели хватает, чтобы выпустить новую.</summary>
    private const int WarnDaysAhead = 14;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PoaExpiryWorker> _logger;

    public PoaExpiryWorker(IServiceScopeFactory scopes, ILogger<PoaExpiryWorker> logger)
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
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось обновить состояния доверенностей");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expired = await db.PowersOfAttorney
            .Where(p => p.Status == PoaStatus.Active && p.ValidTo < today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PoaStatus.Expired)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

        if (expired > 0)
            _logger.LogInformation("Доверенности: закрыто по истечении срока — {Count}", expired);

        var edge = today.AddDays(WarnDaysAhead);

        var soon = await db.PowersOfAttorney
            .AsNoTracking()
            .Where(p => p.Status == PoaStatus.Active && p.ValidTo >= today && p.ValidTo <= edge)
            .Select(p => new {p.RegNumber, p.HolderName, p.ValidTo})
            .ToListAsync(ct);

        foreach (var poa in soon)
        {
            _logger.LogWarning(
                "Доверенность № {Number} на {Holder} истекает {Date:dd.MM.yyyy}",
                poa.RegNumber ?? "б/н", poa.HolderName, poa.ValidTo);
        }
    }
}
