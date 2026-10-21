using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Доска служебных записок: время на стадии, «зависла», срок, свои черновики,
/// и подвкладка реестра «Черновики других пользователей».
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzTrackerBoardTests
{
    private readonly PostgresFixture _postgres;

    public SzTrackerBoardTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Время_на_стадии_сдвигается_только_при_смене_статуса()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор", null);
        var docId = await AddDocumentAsync(db, "Записка", автор.Id, SzStatus.Draft);

        var давно = DateTime.UtcNow.AddDays(-20);
        await db.Documents.Where(d => d.Id == docId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.StatusChangedAt, давно));
        db.ChangeTracker.Clear();

        // Правка полей — не смена стадии.
        var doc = await db.Documents.FirstAsync(d => d.Id == docId);
        doc.Title = "Записка (уточнённая)";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.True((await StageSinceAsync(db, docId)) < DateTime.UtcNow.AddDays(-19));

        // Смена статуса — новая стадия.
        await new DocumentService(db, new AuditService(db), new NumeratorService(db))
            .ChangeStatusAsync(docId, SzStatus.OnApproval, автор.Id);
        db.ChangeTracker.Clear();
        Assert.True((await StageSinceAsync(db, docId)) > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Зависла_считается_по_стадии_а_не_по_дате_создания()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор", null);

        // Месяц лежала черновиком, на согласование ушла позавчера — не зависла.
        var свежая = await AddMemoAsync(db, "Только что на согласовании", автор.Id, null, SzStatus.OnApproval,
            createdDaysAgo: 30, stageDaysAgo: 2);
        // На согласовании десять дней — зависла.
        var застряла = await AddMemoAsync(db, "Давно на согласовании", автор.Id, null, SzStatus.OnApproval,
            createdDaysAgo: 12, stageDaysAgo: 10);

        var доска = await BoardAsync(db, автор.Id);
        var items = доска.Columns.SelectMany(c => c.Items).ToDictionary(i => i.Id);

        Assert.False(items[свежая].IsStale);
        Assert.Equal(2, items[свежая].StageDays);
        Assert.Equal(30, items[свежая].AgeDays);
        Assert.True(items[застряла].IsStale);
    }

    [Fact]
    public async Task На_исполнении_со_сроком_решает_срок()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор", null);
        var today = new BankClock().Today;

        var просрочена = await AddMemoAsync(db, "Просрочена", автор.Id, null, SzStatus.OnExecution,
            createdDaysAgo: 60, stageDaysAgo: 50, due: today.AddDays(-3));
        var безСрока = await AddMemoAsync(db, "Без срока", автор.Id, null, SzStatus.OnExecution,
            createdDaysAgo: 60, stageDaysAgo: 50);

        var items = (await BoardAsync(db, автор.Id)).Columns.SelectMany(c => c.Items).ToDictionary(i => i.Id);

        Assert.True(items[просрочена].IsOverdue);
        Assert.Equal(-3, items[просрочена].DaysLeft);
        Assert.False(items[просрочена].IsStale);
        Assert.True(items[безСрока].IsStale);
    }

    [Fact]
    public async Task На_доске_только_свои_черновики_даже_у_администратора()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var админ = await AddUserAsync(db, "Администратор", null);
        var чужой = await AddUserAsync(db, "Другой автор", null);

        await AddMemoAsync(db, "Мой черновик", админ.Id, null, SzStatus.Draft);
        await AddMemoAsync(db, "Чужой черновик", чужой.Id, null, SzStatus.Draft);
        await AddMemoAsync(db, "Чужая в работе", чужой.Id, null, SzStatus.OnApproval);

        var доска = await BoardAsync(db, админ.Id, PermissionCode.ViewAllSz, PermissionCode.ManageSystemSettings);
        var titles = доска.Columns.SelectMany(c => c.Items).Select(i => i.Title).ToList();

        Assert.Contains("Мой черновик", titles);
        Assert.Contains("Чужая в работе", titles);
        Assert.DoesNotContain("Чужой черновик", titles);
        Assert.True(доска.CanSeeOthersDrafts);
    }

    [Fact]
    public async Task Прежний_статус_Зарегистрирована_и_коллегиальный_орган_не_пропадают_с_доски()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор", null);

        await AddMemoAsync(db, "Старая зарегистрированная", автор.Id, null, SzStatus.Registered);
        await AddMemoAsync(db, "На Правлении", автор.Id, null, SzStatus.OnBoardReview);

        var доска = await BoardAsync(db, автор.Id);

        Assert.Contains(доска.Columns.Single(c => c.Code == SzStatus.OnApproval).Items,
            i => i.Title == "Старая зарегистрированная");
        Assert.Contains(доска.Columns.Single(c => c.Code == SzStatus.OnBoardReview).Items,
            i => i.Title == "На Правлении");
    }

    [Fact]
    public async Task Мои_и_моё_подразделение_помечены()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var отдел = new OrganizationUnit {TitleRu = "Отдел"};
        var другой = new OrganizationUnit {TitleRu = "Другой отдел"};
        db.OrganizationUnits.AddRange(отдел, другой);
        await db.SaveChangesAsync();

        var я = await AddUserAsync(db, "Я", отдел.Id);
        var коллега = await AddUserAsync(db, "Коллега", отдел.Id);

        var моя = await AddMemoAsync(db, "Моя", я.Id, отдел.Id, SzStatus.OnApproval);
        var отдела = await AddMemoAsync(db, "Коллеги", коллега.Id, отдел.Id, SzStatus.OnApproval);
        var чужая = await AddMemoAsync(db, "Чужая", коллега.Id, другой.Id, SzStatus.OnApproval);

        // ViewAllSz — чтобы записки коллег вообще были видны на доске.
        var доска = await BoardAsync(db, я.Id, PermissionCode.ViewAllSz);
        var items = доска.Columns.SelectMany(c => c.Items).ToDictionary(i => i.Id);

        Assert.True(доска.HasUnit);
        Assert.True(items[моя].IsMine);
        Assert.False(items[отдела].IsMine);
        Assert.True(items[отдела].IsMyUnit);
        Assert.False(items[чужая].IsMyUnit);
    }

    [Fact]
    public async Task Черновики_других_пользователей_видит_только_администратор()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var админ = await AddUserAsync(db, "Администратор", null);
        var делопроизводитель = await AddUserAsync(db, "Делопроизводитель", null);
        var чужой = await AddUserAsync(db, "Другой автор", null);

        await AddMemoAsync(db, "Мой черновик", админ.Id, null, SzStatus.Draft);
        await AddMemoAsync(db, "Чужой черновик", чужой.Id, null, SzStatus.Draft);

        var request = new SzSearchRequest
        {
            OthersOnly = true, Statuses = [SzStatus.Draft, SzStatus.Withdrawn], Page = 1, PageSize = 50,
        };

        var уАдмина = await NewSzService(db, админ.Id, PermissionCode.ViewAllSz, PermissionCode.ManageSystemSettings)
            .SearchAsync(request, админ.Id);
        Assert.Equal(new[] {"Чужой черновик"}, уАдмина.Items.Select(i => i.Title).ToList());

        var уДелопроизводства = await NewSzService(db, делопроизводитель.Id, PermissionCode.ViewAllSz)
            .SearchAsync(request, делопроизводитель.Id);
        Assert.Empty(уДелопроизводства.Items);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task<SzTrackerBoardDto> BoardAsync(
        DelosferaDbContext db, int userId, params PermissionCode[] permissions) =>
        await new SzTrackerService(db, new BankClock(), new FakeCurrentUser(userId, permissions))
            .GetBoardAsync(userId);

    private static async Task<DateTime> StageSinceAsync(DelosferaDbContext db, int documentId) =>
        await db.Documents.AsNoTracking().Where(d => d.Id == documentId).Select(d => d.StatusChangedAt).FirstAsync();

    private static ISzService NewSzService(DelosferaDbContext db, int userId, params PermissionCode[] permissions)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var signingProvider = new TestServiceProvider();
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications(), signingProvider);
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));
        signingProvider.Engine = engine;

        return new SzService(db, documents, audit, engine, new PassthroughHtml(),
            new FakeCurrentUser(userId, permissions), handler,
            new SzProcurementService(db, documents, audit, new FakeCurrentUser(userId, PermissionCode.ViewAllSz)),
            new RouteTemplateSelector(db), new BankClock());
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName, int? unitId)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-board-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
            OrgUnitId = unitId,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<int> AddDocumentAsync(DelosferaDbContext db, string title, int authorId, string status)
    {
        var document = new delosfera_server.Modules.Documents.Models.Document
        {
            Type = delosfera_server.Modules.Documents.Models.DocumentType.Sz,
            Title = title,
            StatusCode = status,
            AuthorId = authorId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    /// <summary>Записка в статусе; даты создания и входа в стадию — «столько-то дней назад».</summary>
    private static async Task<int> AddMemoAsync(
        DelosferaDbContext db, string title, int authorId, int? unitId, string status,
        int createdDaysAgo = 0, int stageDaysAgo = 0, DateOnly? due = null)
    {
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();
        var documentId = await AddDocumentAsync(db, title, authorId, status);

        var sz = new SzDocument
        {
            DocumentId = documentId,
            KindId = kind.Id,
            AuthorUnitId = unitId,
            Body = "Текст записки",
            DueDate = due,
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        // Полдень по времени банка: «N дней назад» не съезжает на соседние сутки из-за часового пояса.
        var clock = new BankClock();
        var noon = TimeZoneInfo.ConvertTimeToUtc(clock.Today.ToDateTime(new TimeOnly(12, 0)), clock.Zone);
        await db.Documents.Where(d => d.Id == documentId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.CreatedAt, noon.AddDays(-createdDaysAgo))
            .SetProperty(d => d.StatusChangedAt, noon.AddDays(-stageDaysAgo)));
        db.ChangeTracker.Clear();

        return sz.Id;
    }
}
