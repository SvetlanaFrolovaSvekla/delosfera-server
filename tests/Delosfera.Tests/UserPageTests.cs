using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Страница списка сотрудников.
///
/// Список отдавался целиком: пятьсот сорок учётных записей с ролями, должностью,
/// подразделением и данными о блокировке — семьсот килобайт на каждое открытие
/// страницы, и дальше только больше вместе со штатом.
/// </summary>
[Collection(PostgresCollection.Name)]
public class UserPageTests
{
    private readonly PostgresFixture _postgres;

    public UserPageTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Отдаёт_страницу_а_не_весь_список()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 45);

        var page = await СтраницаАsync(db, page: 1, pageSize: 20, search: признак);

        Assert.Equal(20, page.Items.Count);
        Assert.Equal(45, page.Total);
    }

    [Fact]
    public async Task Страницы_не_повторяют_и_не_теряют_записей()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Полные однофамильцы: без второго признака в сортировке порядок между
        // ними произволен, и одна запись попадает на две страницы, а другая — ни
        // на одну. Такое расходится тихо: количество сходится, состав нет.
        var признак = await ЗавестиСотрудниковАsync(db, 30, одинаковоеИмя: true);

        var первая = await СтраницаАsync(db, page: 1, pageSize: 10, search: признак);
        var вторая = await СтраницаАsync(db, page: 2, pageSize: 10, search: признак);
        var третья = await СтраницаАsync(db, page: 3, pageSize: 10, search: признак);

        var собрано = первая.Items.Concat(вторая.Items).Concat(третья.Items)
            .Select(u => u.Id).ToList();

        Assert.Equal(30, собрано.Count);
        Assert.Equal(30, собрано.Distinct().Count());
    }

    [Fact]
    public async Task Счётчики_считают_по_всем_а_не_по_странице()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 25);
        await ЗаблокироватьАsync(db, признак, 7);

        var page = await СтраницаАsync(db, page: 1, pageSize: 5, search: признак);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(25, page.Counts.All);
        Assert.Equal(18, page.Counts.Active);
        Assert.Equal(7, page.Counts.Blocked);
    }

    [Fact]
    public async Task Счётчики_не_меняются_от_выбранной_вкладки()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 25);
        await ЗаблокироватьАsync(db, признак, 7);

        // Открыта вкладка «Заблокированы» — но на других вкладках должно быть
        // видно, сколько записей ждёт там, иначе по ним не перейти осмысленно.
        var page = await СтраницаАsync(db, page: 1, pageSize: 5, search: признак, isBlocked: true);

        Assert.Equal(25, page.Counts.All);
        Assert.Equal(18, page.Counts.Active);
        Assert.Equal(7, page.Counts.Blocked);
        Assert.Equal(7, page.Total);
    }

    [Fact]
    public async Task Отключённый_в_каталоге_не_считается_работающим()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 10);

        var уволенный = await db.Users.Where(u => u.FullName.Contains(признак))
            .OrderBy(u => u.Id).FirstAsync();
        уволенный.IsActive = false;
        await db.SaveChangesAsync();

        var page = await СтраницаАsync(db, page: 1, pageSize: 20, search: признак);

        // Администратор его не блокировал — но войти он не может, и показывать
        // его работающим значит держать уволенных наравне с действующими.
        Assert.Equal(9, page.Counts.Active);
        Assert.Equal(1, page.Counts.Blocked);
    }

    [Fact]
    public async Task Страница_не_может_вырасти_до_всего_списка()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 60);

        // Запрос со страницей в тысячу записей — тот же полный список, только
        // через параметр: ограничение возвращает к размеру по умолчанию.
        var page = await СтраницаАsync(db, page: 1, pageSize: 100_000, search: признак);

        Assert.Equal(20, page.Items.Count);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Отбор_по_нескольким_источникам_идёт_на_сервере()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 30);

        var изКаталога = await db.Users.Where(u => u.FullName.Contains(признак))
            .OrderBy(u => u.Id).Take(12).ToListAsync();
        foreach (var u in изКаталога) u.Source = UserSource.Ldap;
        await db.SaveChangesAsync();

        var только = await СтраницаАsync(db, page: 1, pageSize: 5,
            search: признак, sources: [UserSource.Ldap]);
        var оба = await СтраницаАsync(db, page: 1, pageSize: 5,
            search: признак, sources: [UserSource.Ldap, UserSource.Local]);

        // Раньше второй источник добирался на клиенте по уже загруженному
        // списку: со страницей такой отбор нашёл бы совпадения только в ней.
        Assert.Equal(12, только.Total);
        Assert.Equal(30, оба.Total);
    }

    [Fact]
    public async Task Поиск_сужает_и_список_и_счётчики()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var признак = await ЗавестиСотрудниковАsync(db, 20);

        var искомый = await db.Users.Where(u => u.FullName.Contains(признак))
            .OrderBy(u => u.Id).FirstAsync();
        искомый.FullName = "Мукушева Дамира";
        await db.SaveChangesAsync();

        var page = await СтраницаАsync(db, page: 1, pageSize: 20, search: "мукушева");

        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Equal(1, page.Counts.All);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task<UserPageResponse> СтраницаАsync(
        DelosferaDbContext db,
        int page,
        int pageSize,
        string? search = null,
        List<UserSource>? sources = null,
        bool? isBlocked = null)
    {
        var service = new UserService(
            db,
            new delosfera_server.Common.Services.Authorization.UserPasswordHasher(),
            new delosfera_server.Common.Security.PasswordPolicy(
                Microsoft.Extensions.Options.Options.Create(
                    new delosfera_server.Common.Security.PasswordPolicyOptions())),
            new delosfera_server.Modules.Documents.Services.AuditService(db),
            new FakeCurrentUser(0));

        return await service.GetPageAsync(
            page, pageSize, UserSortBy.NameAsc, search,
            orgUnitIds: null, positionIds: null, roleIds: null,
            sources: sources, isBlocked: isBlocked, languageCode: "ru");
    }

    /// <summary>
    /// Заводит сотрудников с общим признаком в имени и возвращает его.
    ///
    /// Признак нужен, чтобы отобрать в проверках только заведённых здесь:
    /// база не пуста — учётные записи заводят миграции, и без отбора счётчики
    /// показывали бы их вместе с посеянными.
    /// </summary>
    private static async Task<string> ЗавестиСотрудниковАsync(
        DelosferaDbContext db, int count, bool одинаковоеИмя = false)
    {
        var признак = $"Проба{Guid.NewGuid():N}"[..12];

        for (var i = 0; i < count; i++)
        {
            db.Users.Add(new User
            {
                FullName = одинаковоеИмя ? $"Иванов Иван {признак}" : $"{признак} {i:D3}",
                Email = $"page-{Guid.NewGuid():N}@keremetbank.kg",
                PasswordHash = "x",
                Source = UserSource.Local,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
        return признак;
    }

    private static async Task ЗаблокироватьАsync(DelosferaDbContext db, string признак, int count)
    {
        var users = await db.Users
            .Where(u => u.FullName.Contains(признак))
            .OrderBy(u => u.Id).Take(count).ToListAsync();

        foreach (var u in users)
            u.BlockedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
