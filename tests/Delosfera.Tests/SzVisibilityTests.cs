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
/// Кому какие записки видны в реестре.
///
/// Реестр показывал все записки банка любому вошедшему. Записка часто содержит
/// то, что касается одного подразделения: оклады, перемещения, спорная закупка.
/// Общий реестр на всех означал, что это читает кто угодно.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzVisibilityTests
{
    private readonly PostgresFixture _postgres;

    public SzVisibilityTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Сотрудник_видит_только_свои()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.SotrudnikOtdela);

        Assert.Contains("Записка отдела", видно);
        Assert.DoesNotContain("Записка соседнего отдела", видно);
        Assert.DoesNotContain("Записка другого управления", видно);
    }

    [Fact]
    public async Task Начальник_отдела_видит_свой_отдел()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.NachalnikOtdela);

        // Свой отдел — да; соседний отдел того же управления — нет: он не его.
        Assert.Contains("Записка отдела", видно);
        Assert.DoesNotContain("Записка соседнего отдела", видно);
    }

    [Fact]
    public async Task Начальник_управления_видит_управление_и_вложенные_отделы()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.NachalnikUpravleniya);

        Assert.Contains("Записка отдела", видно);
        Assert.Contains("Записка соседнего отдела", видно);
        Assert.DoesNotContain("Записка другого управления", видно);
    }

    [Fact]
    public async Task Делопроизводство_видит_все_кроме_чужих_черновиков()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Delo, PermissionCode.ViewAllSz);

        Assert.Contains("Записка отдела", видно);
        Assert.Contains("Записка другого управления", видно);
        Assert.DoesNotContain("Чужой черновик", видно);
    }

    [Fact]
    public async Task Администратор_видит_всё_включая_чужие_черновики()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Admin,
            PermissionCode.ViewAllSz, PermissionCode.ManageSystemSettings);

        // Администратор разбирает то, что застряло: слепых зон у него быть не должно.
        Assert.Contains("Чужой черновик", видно);
    }

    [Fact]
    public async Task Согласующий_видит_пришедшую_к_нему_записку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Soglasuyushchiy);

        // Записка чужого управления, но согласовывать её ему: без неё в реестре
        // он не найдёт то, что должен завизировать.
        Assert.Contains("Записка другого управления", видно);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int SotrudnikOtdela, int NachalnikOtdela, int NachalnikUpravleniya,
        int Delo, int Admin, int Soglasuyushchiy);

    /// <summary>Названия записок, видных пользователю в реестре.</summary>
    private static async Task<List<string>> НайтиАsync(
        DelosferaDbContext db, int userId, params PermissionCode[] permissions)
    {
        var service = NewService(db, userId, permissions);

        var page = await service.SearchAsync(new SzSearchRequest
        {
            Page = 1,
            PageSize = 100,
            Statuses = [.. SzStatus.All],
        }, userId);

        return page.Items.Select(i => i.Title).ToList();
    }

    private static ISzService NewService(
        DelosferaDbContext db, int userId, params PermissionCode[] permissions)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures());

        return new SzService(db, documents, audit, engine, new PassthroughHtml(),
            new FakeCurrentUser(userId, permissions), handler,
            new SzProcurementService(db, documents, audit));
    }

    /// <summary>
    /// Управление с двумя отделами, отдельное второе управление и записки в каждом.
    /// </summary>
    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var управление = await AddUnitAsync(db, "Управление делами", null);
        var отдел = await AddUnitAsync(db, "Отдел документооборота", управление.Id);
        var соседний = await AddUnitAsync(db, "Отдел контроля", управление.Id);
        var другое = await AddUnitAsync(db, "Управление рисков", null);

        var сотрудник = await AddUserAsync(db, "Сотрудник отдела", отдел.Id);
        var начОтдела = await AddUserAsync(db, "Начальник отдела", отдел.Id);
        var начУправления = await AddUserAsync(db, "Начальник управления", управление.Id);
        var делопроизводство = await AddUserAsync(db, "Делопроизводитель", null);
        var админ = await AddUserAsync(db, "Администратор", null);
        var согласующий = await AddUserAsync(db, "Согласующий", другое.Id);
        var чужой = await AddUserAsync(db, "Автор чужого черновика", другое.Id);

        отдел.HeadUserId = начОтдела.Id;
        управление.HeadUserId = начУправления.Id;
        await db.SaveChangesAsync();

        await AddMemoAsync(db, "Записка отдела", сотрудник.Id, отдел.Id, SzStatus.OnApproval);
        await AddMemoAsync(db, "Записка соседнего отдела", await AnyUserIdAsync(db, соседний.Id), соседний.Id, SzStatus.OnApproval);
        var чужая = await AddMemoAsync(db, "Записка другого управления", согласующий.Id, другое.Id, SzStatus.OnApproval);
        await AddMemoAsync(db, "Чужой черновик", чужой.Id, другое.Id, SzStatus.Draft);

        // Согласующий видит записку не по подразделению, а потому что она пришла
        // к нему на визу — пусть она и чужого управления.
        db.SzApprovers.Add(new SzApprover {SzDocumentId = чужая, UserId = согласующий.Id, Order = 1});
        await db.SaveChangesAsync();

        return new Стенд(сотрудник.Id, начОтдела.Id, начУправления.Id,
            делопроизводство.Id, админ.Id, согласующий.Id);
    }

    private static async Task<int> AnyUserIdAsync(DelosferaDbContext db, int unitId)
    {
        var user = await AddUserAsync(db, "Сотрудник соседнего отдела", unitId);
        return user.Id;
    }

    private static async Task<OrganizationUnit> AddUnitAsync(
        DelosferaDbContext db, string title, int? parentId)
    {
        var unit = new OrganizationUnit
        {
            TitleRu = title,
            ParentId = parentId,
        };

        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();

        return unit;
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName, int? unitId)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-vis-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
            OrgUnitId = unitId,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    private static async Task<int> AddMemoAsync(
        DelosferaDbContext db, string title, int authorId, int unitId, string status)
    {
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var document = new delosfera_server.Modules.Documents.Models.Document
        {
            Type = delosfera_server.Modules.Documents.Models.DocumentType.Sz,
            Title = title,
            StatusCode = status,
            AuthorId = authorId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument
        {
            DocumentId = document.Id,
            KindId = kind.Id,
            AuthorUnitId = unitId,
            Body = "Текст записки",
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        return sz.Id;
    }
}
