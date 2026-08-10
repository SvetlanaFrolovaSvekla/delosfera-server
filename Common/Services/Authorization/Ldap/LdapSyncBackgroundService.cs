using Microsoft.Extensions.Options;
using delosfera_server.Common.Options;

namespace delosfera_server.Common.Services.Authorization.Ldap;

public class LdapSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LdapOptions _options;
    private readonly ILogger<LdapSyncBackgroundService> _logger;

    public LdapSyncBackgroundService(
        IServiceScopeFactory scopeFactory, IOptions<LdapOptions> options,
        ILogger<LdapSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SyncIntervalMinutes));

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<LdapUserSyncService>();
                await syncService.SyncAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Воркер не должен падать целиком из-за временной недоступности LDAP-сервера —
                // просто логируем и ждём следующего тика
                _logger.LogError(ex, "LDAP sync упал, попробуем на следующем тике");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}