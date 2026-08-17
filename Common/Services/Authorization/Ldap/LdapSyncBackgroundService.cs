using delosfera_server.Modules.Integrations.Directory;

namespace delosfera_server.Common.Services.Authorization.Ldap;

/// <summary>
/// Периодическая выгрузка пользователей из службы каталогов.
///
/// Расписание берётся из настроек в базе и перечитывается перед каждым заходом:
/// администратор меняет интервал через интерфейс, и правка должна вступать в силу
/// сама. Раньше интервал брался из конфигурации при старте, поэтому изменить его
/// можно было только перезапуском сервера.
///
/// Выключенная интеграция не значит остановленную службу: она продолжает
/// просыпаться и проверять настройки, иначе включение потребовало бы перезапуска.
/// </summary>
public class LdapSyncBackgroundService : BackgroundService
{
    /// <summary>Как часто проверять настройки, пока интеграция выключена.</summary>
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LdapSyncBackgroundService> _logger;

    public LdapSyncBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<LdapSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Пауза на старте: миграции и разогрев не должны конкурировать с обходом каталога.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<IDirectorySettingsService>();
                var settings = await settingsService.GetAsync(stoppingToken);

                if (settings.Enabled)
                {
                    delay = TimeSpan.FromMinutes(Math.Clamp(settings.SyncIntervalMinutes, 5, 1440));

                    // Тот же механизм, что и у кнопки «Синхронизировать сейчас»:
                    // он умеет шифрование связи и подтягивает должность с отделом.
                    var syncService = scope.ServiceProvider
                        .GetRequiredService<Modules.Integrations.Directory.IDirectorySyncService>();
                    var result = await syncService.SyncAsync(null, stoppingToken);

                    await settingsService.RecordSyncAsync(
                        result.Created.Count, result.Updated.Count, result.Deactivated.Count,
                        null, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Недоступный каталог не должен ронять службу: ошибка попадает в настройки,
                // администратор видит её в интерфейсе, следующая попытка идёт по расписанию.
                _logger.LogError(ex, "Синхронизация со службой каталогов не удалась");

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var settingsService = scope.ServiceProvider.GetRequiredService<IDirectorySettingsService>();
                    await settingsService.RecordSyncAsync(0, 0, 0, ex.Message, stoppingToken);
                }
                catch (Exception recordFailure)
                {
                    _logger.LogError(recordFailure, "Не удалось записать итог синхронизации");
                }
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
