using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Дефолтный маршрут служебной записки (#15).
///
/// Из коробки ни один вид записки не имел шаблона маршрута, и записка без вручную
/// названных согласующих не уходила на согласование вовсе — SubmitAsync бросал
/// «Не задан маршрут». Глобальный шаблон уровня типа (виза руководителя автора)
/// и его использование как fallback в SubmitAsync закрывают этот тупик.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SzDefaultRouteTests
{
    private readonly PostgresFixture _postgres;

    public SzDefaultRouteTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Сид_заводит_один_глобальный_шаблон_с_визой_руководителя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Идемпотентность: второй вызов не должен плодить дубли.
        await SzRouteTemplateSeeder.SeedAsync(db);
        await SzRouteTemplateSeeder.SeedAsync(db);

        var templates = await db.RouteTemplates
            .Include(t => t.Steps).ThenInclude(s => s.Participants)
            .Where(t => t.DocumentType == DocumentType.Sz && t.OrgUnitId == null)
            .ToListAsync();

        var template = Assert.Single(templates);
        var step = Assert.Single(template.Steps);
        Assert.Equal(StepKind.Approval, step.Kind);
        var participant = Assert.Single(step.Participants);
        Assert.Equal(RouteRoles.AuthorHead, participant.RoleRef);
    }

    [Fact]
    public async Task Записка_без_согласующих_уходит_на_визу_руководителя_автора()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        await SzRouteTemplateSeeder.SeedAsync(db);

        var (service, draftId, headId, documentId) = await SeedDraftWithoutApproversAsync(db);

        // Раньше здесь был throw «Не задан маршрут»; теперь маршрут берётся из
        // глобального шаблона.
        var details = await service.SubmitAsync(draftId, await AuthorOfAsync(db, draftId));

        Assert.Equal(SzStatus.OnApproval, details.StatusCode);

        // Единственный согласующий маршрута — руководитель подразделения автора.
        var participant = await db.RouteParticipants.AsNoTracking()
            .Where(p => p.RouteStep!.RouteInstance!.DocumentId == documentId)
            .SingleAsync();
        Assert.Equal(headId, participant.UserId);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task<(ISzService Service, int DraftId, int HeadId, int DocumentId)>
        SeedDraftWithoutApproversAsync(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var documents = new DocumentService(db, audit, new NumeratorService(db));
        var handler = new SzRouteCompletionHandler(db, documents, audit, new SilentNotifications());
        var engine = new RouteEngine(db, audit, [handler], new NoSubstitutions(), new SilentNotifier(),
            new FakeSignatures(), new RouteRoleResolver(db));
        var currentUser = new FakeCurrentUser(0, PermissionCode.ViewAllSz);
        var procurement = new SzProcurementService(db, documents, audit, currentUser);

        var service = new SzService(db, documents, audit, engine, new PassthroughHtml(),
            currentUser, handler, procurement, new RouteTemplateSelector(db));

        var head = await AddUserAsync(db, "Руководитель отдела");

        // Подразделение автора с назначенным руководителем — его визу и берёт
        // дефолтный шаблон (RoleRef=author-head).
        var unit = new OrganizationUnit { TitleRu = "Отдел разработки", HeadUserId = head.Id };
        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();

        var author = new User
        {
            FullName = "Автор записки",
            Email = $"sz-def-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
            OrgUnitId = unit.Id,
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var addressee = await AddUserAsync(db, "Адресат записки");
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        // Черновик без ApproverUserIds — согласующих автор не называет.
        var draft = await service.CreateDraftAsync(new SzSaveRequest
        {
            Title = "Записка без согласующих",
            KindId = kind.Id,
            Body = "Текст записки",
            AddresseeUserId = addressee.Id,
        }, author.Id);

        var documentId = await db.SzDocuments.AsNoTracking()
            .Where(x => x.Id == draft.Id).Select(x => x.DocumentId).SingleAsync();

        return (service, draft.Id, head.Id, documentId);
    }

    private static async Task<int> AuthorOfAsync(DelosferaDbContext db, int szId) =>
        await db.SzDocuments.AsNoTracking()
            .Where(x => x.Id == szId).Select(x => x.Document!.AuthorId).SingleAsync();

    private static async Task<User> AddUserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sz-def-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
