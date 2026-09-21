using System.Text;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Common.Services.Authorization;

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

            // Прогрев данных снимает только цену первого SQL. Открытие хаба выбора
            // человека (GET /api/users/lookup) платило ~2.5 с и после него: львиную
            // долю занимает разовая работа HTTP-конвейера — JIT контура авторизации,
            // контроллера, сортировки со сравнением по ru-RU и сериализации ~1000
            // объектов в JSON. Эту работу база не греет. Поэтому дергаем сам эндпоинт
            // изнутри процесса: разовая компиляция достаётся фоновой задаче, а не
            // первому сотруднику, открывшему форму после выкладки.
            await WarmHttpAsync(scope.ServiceProvider, db, stoppingToken);
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
    /// Разовый холостой вызов боевого HTTP-эндпоинта изнутри процесса. Kestrel к
    /// моменту вызова уже слушает (прогрев данных до этого занял секунды), но на
    /// всякий случай — короткий цикл повторов на отказ соединения. Токен выпускаем
    /// первому активному пользователю: эндпоинт требует лишь [Authorize], без права.
    /// </summary>
    private async Task WarmHttpAsync(IServiceProvider sp, DelosferaDbContext db, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        try
        {
            // Греем от имени администратора: большинство горячих эндпоинтов защищены правами
            // (RequirePermission), и токен без прав упёрся бы в 403 до тела действия — тогда
            // JIT самого запроса (EF→SQL, сериализация) не прогрелся бы. С правами SEC-1
            // (TokenRevocationValidator) пропустит, и прогреется настоящий конвейер.
            var adminCode = (int)delosfera_server.Modules.Users.Models.PermissionCode.ManageSystemSettings;
            var user = await db.Users.AsNoTracking()
                           .Where(u => u.IsActive && u.BlockedAt == null && u.Email != null
                                       && u.Roles.Any(r => r.PermissionCodes.Contains(adminCode)))
                           .OrderBy(u => u.Id)
                           .FirstOrDefaultAsync(ct)
                       ?? await db.Users.AsNoTracking()
                           .Where(u => u.IsActive && u.BlockedAt == null && u.Email != null)
                           .OrderBy(u => u.Id)
                           .FirstOrDefaultAsync(ct);
            if (user is null) return; // некому выпускать токен — греть нечего

            var jwt = sp.GetRequiredService<IJwtTokenService>();
            var config = sp.GetRequiredService<IConfiguration>();
            var token = jwt.GenerateAccessToken(user, new List<int>());

            // Внутри контейнера сервер слушает 8080 (EXPOSE 8080). Переопределяемо
            // конфигом на случай иной привязки.
            var baseUrl = config["SelfWarm:BaseUrl"] ?? "http://localhost:8080";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 1) Дождаться готовности Kestrel и прогреть первый эндпоинт (он же — контур авторизации).
            var ready = false;
            for (var attempt = 1; attempt <= 10 && !ready; attempt++)
            {
                try
                {
                    var resp = await http.GetAsync($"{baseUrl}/api/users/lookup", ct);
                    await resp.Content.ReadAsByteArrayAsync(ct);
                    ready = true;
                }
                catch (HttpRequestException) when (attempt < 10)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct); // Kestrel ещё поднимается
                }
            }
            if (!ready) return;

            // 2) Прогреть горячие эндпоинты: JIT конвейера (EF→SQL, сериализация) каждого платит
            // фоновая задача, а не первый пользователь после выкладки. Каждый — best-effort:
            // непрогретый эндпоинт просто останется «холодным», это не ошибка запуска.
            var warm = new (string Method, string Path, string? Body)[]
            {
                ("GET", "/api/org-tree", null),
                ("GET", "/api/dictionaries/organization-unit", null),
                ("GET", "/api/dictionaries/coordination-users", null),
                ("GET", "/api/dictionaries/position", null),
                ("GET", "/api/dictionaries/keyword", null),
                ("GET", "/api/dictionaries/rubric", null),
                ("GET", "/api/dictionaries/sz-rubric", null),
                ("GET", "/api/dictionaries/type-vnd", null),
                ("GET", "/api/dictionaries/security-level", null),
                ("GET", "/api/dictionaries/user-group", null),
                ("GET", "/api/dictionaries/approval-body", null),
                ("GET", "/api/users?page=1&pageSize=50", null),
                ("GET", "/api/users/approvers", null),
                ("GET", "/api/hr/orders", null),
                ("GET", "/api/meetings", null),
                ("GET", "/api/obligations", null),
                ("GET", "/api/procurement/suppliers", null),
                ("GET", "/api/procurement/tracker", null),
                ("GET", "/api/substitution-requests", null),
                ("GET", "/api/calendar", null),
                ("GET", "/api/digest", null),
                ("GET", "/api/settings/changes", null),
                ("GET", "/api/acknowledgements/mine", null),
                ("GET", "/api/saved-filters", null),
                ("POST", "/api/search", "{\"query\":\"\"}"),
            };

            var warmed = 0;
            foreach (var (method, path, body) in warm)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var msg = new HttpRequestMessage(new HttpMethod(method), $"{baseUrl}{path}");
                    if (body is not null)
                        msg.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    var resp = await http.SendAsync(msg, ct);
                    await resp.Content.ReadAsByteArrayAsync(ct);
                    warmed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // best-effort: один непрогретый эндпоинт не срывает прогрев остальных
                }
            }

            _logger.LogInformation(
                "Прогрев HTTP: {Warmed}/{Total} эндпоинтов за {Ms} мс",
                warmed + 1, warm.Length + 1, (int)(DateTime.UtcNow - started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Прогрев HTTP не удался");
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

        // Список для выбора человека (GET /api/users/lookup) открывается в каждой форме
        // с пикером, а его запрос отличается формой от прогретого выше (полный список с
        // проекцией должности и подразделения, без ролей). Без прогрева именно этой формы
        // первое открытие любой формы с выбором человека платило ~2.5 с на компиляцию плана
        // и чтение страниц; греем точную форму — и флаги старшинства из тех же таблиц.
        await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.BlockedAt == null)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                position = u.Position != null ? u.Position.TitleRu : null,
                orgUnit = u.OrgUnit != null ? u.OrgUnit.TitleRu : null,
                u.OrgUnitId,
            })
            .ToListAsync(ct);

        await db.BodyMembers
            .AsNoTracking()
            .Where(m => m.Body == delosfera_server.Modules.Meetings.Models.MeetingBody.Board)
            .Select(m => new { m.UserId, m.Role })
            .ToListAsync(ct);

        await db.OrganizationUnits
            .AsNoTracking()
            .Where(o => o.HeadUserId != null)
            .Select(o => o.HeadUserId)
            .ToListAsync(ct);
    }
}
