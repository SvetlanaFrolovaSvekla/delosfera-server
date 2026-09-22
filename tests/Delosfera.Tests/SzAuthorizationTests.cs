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
/// Кто вправе править, удалять, отправлять чужую служебную записку и менять ей
/// маршрут согласующих.
///
/// Раньше сервис грузил записку по {id}, проверял только статус, а идентификатор
/// вызывающего шёл лишь в аудит — и любой вошедший мог переписать, удалить или
/// протолкнуть чужой черновик по его номеру (IDOR). Действие ведёт автор; в
/// застрявшую записку могут вмешаться держатель реестра СЗ целиком (ViewAllSz)
/// и администратор системы (ManageSystemSettings), остальным — 403.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzAuthorizationTests
{
    private readonly PostgresFixture _postgres;

    public SzAuthorizationTests(PostgresFixture postgres) => _postgres = postgres;

    // ── PUT /api/sz/{id} — правка ──────────────────────────────────────────────

    [Fact]
    public async Task Автор_может_редактировать_свой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var details = await Service(db, стенд.Author)
            .UpdateDraftAsync(стенд.SzId, SaveRequest(стенд, "Новая тема"), стенд.Author);

        Assert.Equal("Новая тема", details.Title);
    }

    [Fact]
    public async Task Чужой_не_может_редактировать_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(db, стенд.Stranger)
                .UpdateDraftAsync(стенд.SzId, SaveRequest(стенд, "Взлом"), стенд.Stranger));
    }

    [Fact]
    public async Task Держатель_ViewAllSz_может_редактировать_чужой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var details = await Service(db, стенд.Stranger, PermissionCode.ViewAllSz)
            .UpdateDraftAsync(стенд.SzId, SaveRequest(стенд, "Правка реестром"), стенд.Stranger);

        Assert.Equal("Правка реестром", details.Title);
    }

    // ── DELETE /api/sz/{id} — удаление ─────────────────────────────────────────

    [Fact]
    public async Task Автор_может_удалить_свой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Service(db, стенд.Author).DeleteDraftAsync(стенд.SzId, стенд.Author);

        Assert.False(await db.SzDocuments.AnyAsync(x => x.Id == стенд.SzId));
    }

    [Fact]
    public async Task Чужой_не_может_удалить_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(db, стенд.Stranger).DeleteDraftAsync(стенд.SzId, стенд.Stranger));

        Assert.True(await db.SzDocuments.AnyAsync(x => x.Id == стенд.SzId));
    }

    [Fact]
    public async Task Администратор_ManageSystemSettings_может_удалить_чужой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Service(db, стенд.Stranger, PermissionCode.ManageSystemSettings)
            .DeleteDraftAsync(стенд.SzId, стенд.Stranger);

        Assert.False(await db.SzDocuments.AnyAsync(x => x.Id == стенд.SzId));
    }

    // ── POST /api/sz/{id}/submit — отправка ────────────────────────────────────

    [Fact]
    public async Task Автор_может_отправить_свой_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var details = await Service(db, стенд.Author).SubmitAsync(стенд.SzId, стенд.Author);

        Assert.Equal(SzStatus.PendingRegistration, details.StatusCode);
    }

    [Fact]
    public async Task Чужой_не_может_отправить_черновик()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(db, стенд.Stranger).SubmitAsync(стенд.SzId, стенд.Stranger));
    }

    // ── PUT /api/sz/{id}/approvers — маршрут согласующих ───────────────────────

    [Fact]
    public async Task Автор_может_задать_согласующих()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var details = await Service(db, стенд.Author)
            .SetApproversAsync(стенд.SzId, [стенд.Stranger], parallel: false, стенд.Author);

        Assert.Single(details.Approvers);
    }

    [Fact]
    public async Task Чужой_не_может_переписать_согласующих()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(db, стенд.Stranger)
                .SetApproversAsync(стенд.SzId, [стенд.Stranger], parallel: false, стенд.Stranger));
    }

    // ── стенд ──────────────────────────────────────────────────────────────────

    private sealed record Стенд(int Author, int Stranger, int KindId, int SzId);

    private static SzSaveRequest SaveRequest(Стенд стенд, string title) => new()
    {
        Title = title,
        KindId = стенд.KindId,
        Body = "Текст записки",
        AddresseeUserId = стенд.Stranger,
    };

    private static ISzService Service(
        DelosferaDbContext db, int actorId, params PermissionCode[] permissions)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var signingProvider = new TestServiceProvider();
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications(), signingProvider);
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));
        signingProvider.Engine = engine;

        return new SzService(db, documents, audit, engine, new PassthroughHtml(),
            new FakeCurrentUser(actorId, permissions), handler,
            new SzProcurementService(db, documents, audit, new FakeCurrentUser(actorId, permissions)),
            new RouteTemplateSelector(db), new BankClock());
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();
        var author = await AddUserAsync(db, "Автор записки");
        var stranger = await AddUserAsync(db, "Посторонний");

        var document = new delosfera_server.Modules.Documents.Models.Document
        {
            Type = delosfera_server.Modules.Documents.Models.DocumentType.Sz,
            Title = "Исходная тема",
            StatusCode = SzStatus.Draft,
            AuthorId = author.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument
        {
            DocumentId = document.Id,
            KindId = kind.Id,
            AuthorUnitId = author.OrgUnitId,
            Body = "Текст записки",
            AddresseeUserId = stranger.Id,
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        return new Стенд(author.Id, stranger.Id, kind.Id, sz.Id);
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-authz-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
