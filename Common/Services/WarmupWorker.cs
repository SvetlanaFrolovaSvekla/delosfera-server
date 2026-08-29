using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Common.Services;

/// <summary>
/// Прогревает доступ к данным сразу после запуска.
///
/// Первое обращение к разделу после перезапуска занимало пять секунд, из них в
/// базу уходило шестьсот миллисекунд. Остальное — разовая работа: EF переводит
/// дерево запроса в SQL, собирает материализатор, а среда исполнения впервые
/// компилирует этот код. Плата разовая, но достаётся она человеку, который
/// первым открыл раздел после выкладки.
///
/// Здесь та же работа делается заранее и вхолостую. Запросы повторяют форму
/// боевых — фильтр, сортировка, страница, счётчик, — а не вызывают сами службы:
/// те опираются на данные вошедшего пользователя, которых у фоновой задачи нет.
/// Поэтому прогрев снимает общую часть, а не заменяет первый настоящий запрос.
/// </summary>
public class WarmupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WarmupWorker> _logger;

    public WarmupWorker(IServiceScopeFactory scopes, ILogger<WarmupWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = DateTime.UtcNow;

        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();

            await WarmAsync(db, stoppingToken);

            _logger.LogInformation(
                "Прогрев доступа к данным занял {Ms} мс",
                (int)(DateTime.UtcNow - started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Приложение останавливают — прогревать нечего.
        }
        catch (Exception ex)
        {
            // Прогрев — удобство, а не условие работы. Если он не удался,
            // первый запрос просто окажется медленным, как раньше.
            _logger.LogWarning(ex, "Прогрев доступа к данным не удался");
        }
    }

    /// <summary>
    /// Холостые запросы по форме боевых. Открыт для проверки: прогрев выполняется
    /// на старте, и падение здесь означало бы приложение, которое не поднимается
    /// из-за подготовки, без которой оно прекрасно работает.
    /// </summary>
    public static async Task WarmAsync(DelosferaDbContext db, CancellationToken ct = default)
    {
        // Основа всех реестров: отбор по виду и состоянию, сортировка, страница
        // и счётчик для постраничной навигации.
        foreach (var type in new[] {DocumentType.Sz, DocumentType.Vnd, DocumentType.Procurement})
        {
            await db.Documents
                .AsNoTracking()
                .Where(d => d.Type == type)
                .OrderByDescending(d => d.CreatedAt)
                .Skip(0).Take(20)
                .Select(d => new {d.Id, d.Title, d.RegNumber, d.StatusCode, d.CreatedAt})
                .ToListAsync(ct);

            await db.Documents.AsNoTracking().CountAsync(d => d.Type == type, ct);
        }

        // Карточки контуров: связь с документом и проекция — самая частая форма.
        await db.SzDocuments
            .AsNoTracking()
            .Include(x => x.Document)
            .OrderByDescending(x => x.Id)
            .Take(20)
            .ToListAsync(ct);

        await db.ProcurementRequests
            .AsNoTracking()
            .Include(x => x.Document)
            .OrderByDescending(x => x.Id)
            .Take(20)
            .ToListAsync(ct);

        await db.VndDocuments
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(20)
            .ToListAsync(ct);

        // Права проверяются на каждом запросе, справочники подставляются в
        // каждой форме — они греются первыми в любом сценарии работы.
        await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Take(20)
            .ToListAsync(ct);

        await db.OrganizationUnits.AsNoTracking().OrderBy(o => o.TitleRu).Take(50).ToListAsync(ct);
        await db.Positions.AsNoTracking().OrderBy(p => p.TitleRu).Take(50).ToListAsync(ct);

        // Маршруты согласования: через них проходит и задача в очереди, и кнопка
        // на карточке — то есть почти каждое действие после открытия раздела.
        await db.RouteSteps
            .AsNoTracking()
            .Include(s => s.Participants)
            .OrderByDescending(s => s.Id)
            .Take(20)
            .ToListAsync(ct);
    }
}
