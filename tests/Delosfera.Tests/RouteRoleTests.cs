using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Согласующий, заданный в шаблоне ролью, превращается в человека при запуске.
///
/// Роль копировалась в участника маршрута как есть и человеком не становилась
/// никогда: задача не создавалась, участник числился активным, и маршрут вставал
/// навсегда — молча, потому что формально всё было в порядке.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RouteRoleTests
{
    private readonly PostgresFixture _postgres;

    public RouteRoleTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Роль_руководителя_превращается_в_человека()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var маршрут = await ЗапуститьАsync(db, стенд, RouteRoles.AuthorHead);

        Assert.Equal(стенд.Начальник, Участник(маршрут));
    }

    [Fact]
    public async Task Роль_куратора_превращается_в_человека()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var маршрут = await ЗапуститьАsync(db, стенд, RouteRoles.AuthorCurator);

        Assert.Equal(стенд.Куратор, Участник(маршрут));
    }

    [Fact]
    public async Task Роль_председателя_берётся_из_состава_Правления()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var маршрут = await ЗапуститьАsync(db, стенд, RouteRoles.BoardChairman);

        Assert.Equal(стенд.Председатель, Участник(маршрут));
    }

    [Fact]
    public async Task Роль_конкретного_подразделения_разрешается_по_номеру()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var маршрут = await ЗапуститьАsync(db, стенд, $"{RouteRoles.UnitHeadPrefix}{стенд.Подразделение}");

        Assert.Equal(стенд.Начальник, Участник(маршрут));
    }

    [Fact]
    public async Task Незаполненный_справочник_останавливает_запуск_внятно()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Куратора у подразделения нет — маршрут не должен молча повиснуть.
        await db.OrganizationUnits.Where(u => u.Id == стенд.Подразделение)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CuratorUserId, (int?) null));

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ЗапуститьАsync(db, стенд, RouteRoles.AuthorCurator));

        Assert.Contains("куратор подразделения автора", ошибка.Message);
    }

    [Fact]
    public async Task Поимённый_участник_ролью_не_подменяется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // У кого задан человек — тот и остаётся, даже если роль тоже указана.
        var маршрут = await ЗапуститьАsync(db, стенд, RouteRoles.AuthorHead, userId: стенд.Автор);

        Assert.Equal(стенд.Автор, Участник(маршрут));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int DocumentId, int Автор, int Начальник, int Куратор, int Председатель, int Подразделение);

    private static int? Участник(RouteInstance instance) =>
        instance.Steps.Single().Participants.Single().UserId;

    private static async Task<RouteInstance> ЗапуститьАsync(
        DelosferaDbContext db, Стенд стенд, string roleRef, int? userId = null)
    {
        var template = new RouteTemplate
        {
            DocumentType = DocumentType.Sz,
            Name = "Проверка ролей",
            Steps =
            [
                new RouteTemplateStep
                {
                    Order = 1,
                    Participants = [new RouteTemplateParticipant {RoleRef = roleRef, UserId = userId}],
                },
            ],
        };
        db.RouteTemplates.Add(template);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var engine = new RouteEngine(
            db, audit, [], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(),
            new RouteRoleResolver(db));

        return await engine.InstantiateFromTemplateAsync(стенд.DocumentId, template.Id);
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var начальник = await ПользовательАsync(db, "Начальник подразделения", null);
        var куратор = await ПользовательАsync(db, "Курирующий зампред", null);
        var председатель = await ПользовательАsync(db, "Председатель Правления", null);

        var unit = new OrganizationUnit
        {
            TitleRu = $"Подразделение {Guid.NewGuid():N}"[..26],
            HeadUserId = начальник,
            CuratorUserId = куратор,
        };
        db.OrganizationUnits.Add(unit);
        db.BodyMembers.Add(new BodyMember
        {
            Body = MeetingBody.Board, UserId = председатель, Role = BodyRole.Chairman,
        });
        await db.SaveChangesAsync();

        var автор = await ПользовательАsync(db, "Автор записки", unit.Id);

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Записка для проверки ролей",
            StatusCode = "Draft",
            AuthorId = автор,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return new Стенд(document.Id, автор, начальник, куратор, председатель, unit.Id);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName, int? unitId)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"role-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
            OrgUnitId = unitId,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
