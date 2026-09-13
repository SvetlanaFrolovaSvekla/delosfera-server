using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Держит ActualizationThresholds (статический in-memory кэш порогов Critical/Approaching,
/// см. ActualizationBucket.cs) в актуальном состоянии.
///
/// Раньше кэш обновлялся ТОЛЬКО когда кто-то вызывал ActualizationBucketSettingsService
/// (открывал справочник настроек или сохранял его) — из-за этого после рестарта процесса
/// (или на реплике, которую ни разу не трогали через этот справочник) кэш откатывался на
/// захардкоженный дефолт 5/30 дней независимо от того, что реально сохранено в БД, и мог
/// оставаться в таком состоянии сколь угодно долго. При нескольких репликах приложения это
/// ещё и рассинхронизация между ними: настройки поменяли на одной — остальные как считали
/// по старым/дефолтным порогам, так и продолжают, пока кто-нибудь не откроет справочник
/// именно на них.
///
/// Этот воркер читает актуальные пороги из БД сразу при старте процесса и затем каждые
/// RefreshInterval — после рестарта кэш не может остаться на дефолте дольше одного тика, а
/// расхождение между репликами ограничено сверху этим интервалом. Мгновенная синхронизация
/// "сразу после сохранения на всех репликах" потребовала бы шины уведомлений между ними
/// (например, Postgres LISTEN/NOTIFY) — избыточно ради значения, которое меняют раз в
/// много месяцев; ActualizationBucketSettingsService по-прежнему обновляет кэш немедленно
/// на своей реплике при сохранении, этот воркер лишь страхует остальные и сам факт рестарта.
/// </summary>
public class ActualizationThresholdsRefreshWorker : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActualizationThresholdsRefreshWorker> _logger;

    public ActualizationThresholdsRefreshWorker(
        IServiceScopeFactory scopeFactory, ILogger<ActualizationThresholdsRefreshWorker> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

                // Тонкий прямой запрос вместо ActualizationBucketSettingsService.GetAsync():
                // нужны только значения, без DTO и без страховки на пустую таблицу (она есть
                // в самом сервисе — если таблица пуста, следующий тик подхватит строку,
                // которую тем временем создаст первое обращение к справочнику).
                var settings = await db.Set<ActualizationBucketSettings>()
                    .AsNoTracking()
                    .Select(x => new { x.CriticalDays, x.ApproachingDays })
                    .FirstOrDefaultAsync(stoppingToken);

                if (settings is not null)
                    ActualizationThresholds.Configure(settings.CriticalDays, settings.ApproachingDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ActualizationThresholdsRefreshWorker: не удалось обновить кэш порогов актуализации");
            }

            await Task.Delay(RefreshInterval, stoppingToken);
        }
    }
}
