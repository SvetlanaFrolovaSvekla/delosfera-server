using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Реестр и ход записки: восстановление из архива, счётчики вкладок, поиск по тексту,
/// просрочка закрытых записок, рубрикатор СЗ и срок поручения.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzRegistryFixesTests
{
    private readonly PostgresFixture _postgres;

    public SzRegistryFixesTests(PostgresFixture postgres) => _postgres = postgres;

    // ── архив ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SzStatus.Rejected)]
    [InlineData(SzStatus.Withdrawn)]
    [InlineData(SzStatus.Executed)]
    public async Task Из_архива_записка_возвращается_в_прежний_статус(string before)
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        var sz = await AddSzAsync(db, автор.Id, SzStatus.OnApproval);

        var documents = Documents(db);
        await documents.ChangeStatusAsync(sz.DocumentId, before, автор.Id);
        await documents.ChangeStatusAsync(sz.DocumentId, SzStatus.Archived, автор.Id);

        var archive = new SzArchiveService(db, documents, new AuditService(db),
            new FakeCurrentUser(автор.Id, PermissionCode.ViewAllSz), new BankClock());

        await archive.RestoreAsync(sz.Id, автор.Id);

        var status = await db.Documents.AsNoTracking()
            .Where(d => d.Id == sz.DocumentId).Select(d => d.StatusCode).SingleAsync();
        Assert.Equal(before, status);
    }

    [Fact]
    public async Task Из_архива_без_следа_в_журнале__Исполнена()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        // Записка заведена сразу архивной — журнал о прежнем статусе ничего не знает.
        var sz = await AddSzAsync(db, автор.Id, SzStatus.Archived);

        var archive = new SzArchiveService(db, Documents(db), new AuditService(db),
            new FakeCurrentUser(автор.Id, PermissionCode.ViewAllSz), new BankClock());
        await archive.RestoreAsync(sz.Id, автор.Id);

        var status = await db.Documents.AsNoTracking()
            .Where(d => d.Id == sz.DocumentId).Select(d => d.StatusCode).SingleAsync();
        Assert.Equal(SzStatus.Executed, status);
    }

    // ── счётчики ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Счётчики_по_статусам_считают_только_видимые_записки()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        var посторонний = await AddUserAsync(db, "Посторонний");
        await AddSzAsync(db, автор.Id, SzStatus.Archived);
        await AddSzAsync(db, автор.Id, SzStatus.OnApproval);

        var чужие = await Service(db, посторонний.Id).CountByStatusAsync(посторонний.Id);
        Assert.Empty(чужие);

        var свои = await Service(db, автор.Id).CountByStatusAsync(автор.Id);
        Assert.Equal(1, свои[SzStatus.Archived]);
        Assert.Equal(1, свои[SzStatus.OnApproval]);

        // Держатель реестра целиком видит обе.
        var реестр = await Service(db, посторонний.Id, PermissionCode.ViewAllSz).CountByStatusAsync(посторонний.Id);
        Assert.Equal(2, реестр.Values.Sum());
    }

    // ── поиск ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Поиск_идёт_по_тексту_записки_а_не_по_разметке()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        await AddSzAsync(db, автор.Id, SzStatus.OnApproval,
            body: "<p><strong>Срочно</strong> согласовать оклад</p>", bodyText: "Срочно согласовать оклад");

        var service = Service(db, автор.Id);

        Assert.Equal(1, (await service.SearchAsync(new SzSearchRequest {Query = "оклад"}, автор.Id)).Total);
        Assert.Equal(0, (await service.SearchAsync(new SzSearchRequest {Query = "strong"}, автор.Id)).Total);
    }

    // ── просрочка ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SzStatus.Archived)]
    [InlineData(SzStatus.Rejected)]
    [InlineData(SzStatus.Withdrawn)]
    [InlineData(SzStatus.Executed)]
    public async Task Закрытая_записка_с_прошедшим_сроком_не_просрочена(string status)
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        var past = new BankClock().Today.AddDays(-10);
        await AddSzAsync(db, автор.Id, status, dueDate: past);

        var service = Service(db, автор.Id);
        var page = await service.SearchAsync(new SzSearchRequest {Statuses = [status]}, автор.Id);

        var item = Assert.Single(page.Items);
        Assert.False(item.IsOverdue);
        Assert.Null(item.DaysLeft);

        var overdue = await service.SearchAsync(new SzSearchRequest {Statuses = [status], OverdueOnly = true}, автор.Id);
        Assert.Equal(0, overdue.Total);
    }

    [Fact]
    public async Task Записка_в_работе_с_прошедшим_сроком_просрочена()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        await AddSzAsync(db, автор.Id, SzStatus.OnExecution, dueDate: new BankClock().Today.AddDays(-3));

        var page = await Service(db, автор.Id)
            .SearchAsync(new SzSearchRequest {OverdueOnly = true}, автор.Id);

        var item = Assert.Single(page.Items);
        Assert.True(item.IsOverdue);
        Assert.Equal(-3, item.DaysLeft);
    }

    // ── рубрики ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Рубрики_записки_берутся_из_рубрикатора_СЗ()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await AddUserAsync(db, "Автор");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var рубрикаСз = new SzRubric {TitleRu = "Оплата труда"};
        db.SzRubrics.Add(рубрикаСз);
        await db.SaveChangesAsync();

        var service = Service(db, автор.Id);
        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Об окладах", KindId = kind.Id, Body = "Текст", RubricIds = [рубрикаСз.Id],
        }, автор.Id);

        Assert.Equal([рубрикаСз.Id], draft.RubricIds);
        Assert.Equal(["Оплата труда"], draft.Rubrics);

        // Фильтр реестра по рубрике — тоже по рубрикатору СЗ.
        var found = await service.SearchAsync(new SzSearchRequest {RubricId = рубрикаСз.Id}, автор.Id);
        Assert.Equal(1, found.Total);
    }

    // ── срок поручения ───────────────────────────────────────────────────────

    [Fact]
    public async Task Поручение_со_сроком_в_прошлом_не_выдаётся()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var адресат = await AddUserAsync(db, "Адресат");
        var исполнитель = await AddUserAsync(db, "Исполнитель");
        var sz = await AddSzAsync(db, адресат.Id, SzStatus.OnExecution);

        var execution = new SzExecutionService(db, Documents(db), new AuditService(db),
            new FakeCurrentUser(адресат.Id), new BankClock());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            execution.ResolveAsync(sz.Id, new SzResolutionRequest
            {
                Text = "Поручаю",
                Assignments =
                [
                    new SzAssignmentRequest
                    {
                        AssigneeUserId = исполнитель.Id, Text = "Сделать", IsPrimary = true,
                        DueDate = new BankClock().Today.AddDays(-1),
                    },
                ],
            }, адресат.Id));

        Assert.Contains("в прошлом", error.Message);
        Assert.False(await db.SzAssignments.AnyAsync(a => a.SzDocumentId == sz.Id));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static DocumentService Documents(DelosferaDbContext db) =>
        new(db, new AuditService(db), new NumeratorService(db));

    private static ISzService Service(DelosferaDbContext db, int userId, params PermissionCode[] permissions)
    {
        var audit = new AuditService(db);
        var documents = Documents(db);
        var currentUser = new FakeCurrentUser(userId, permissions);
        var signingProvider = new TestServiceProvider();
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications(), signingProvider);
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(),
            new FakeSignatures(), new RouteRoleResolver(db));
        signingProvider.Engine = engine;

        return new SzService(db, documents, audit, engine, new PassthroughHtml(), currentUser, handler,
            new SzProcurementService(db, documents, audit, currentUser),
            new RouteTemplateSelector(db), new BankClock());
    }

    private static async Task<SzDocument> AddSzAsync(
        DelosferaDbContext db, int authorId, string status,
        DateOnly? dueDate = null, string body = "Текст", string? bodyText = null)
    {
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Sz, Title = "Служебная записка", StatusCode = status, AuthorId = authorId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument
        {
            DocumentId = document.Id, KindId = kind.Id, Body = body, BodyText = bodyText ?? body,
            DueDate = dueDate,
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();
        return sz;
    }

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-registry-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
