using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Из кого собирается маршрут заявки на закупку.
///
/// Этапы названы по подразделениям, и по названию в карточке не видно, кому
/// задача ушла на самом деле. Два последних этапа звались «Сектор закупок», а
/// людей брали из Административного отдела: заявка уходила не туда, и сам
/// сектор её не видел.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProcurementRouteCompositionTests
{
    private readonly PostgresFixture _postgres;

    public ProcurementRouteCompositionTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Процедуру_ведёт_Сектор_закупок_а_не_Административный_отдел()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var участники = await МаршрутАsync(db, стенд);

        // Предпоследний этап — куратор Сектора закупок, последний — сам сектор.
        Assert.Equal(стенд.КураторЗакупок, участники[^2]);
        Assert.Equal(стенд.РуководительЗакупок, участники[^1]);

        Assert.DoesNotContain(стенд.РуководительАдмин, участники);
        Assert.DoesNotContain(стенд.КураторАдмин, участники);
    }

    [Fact]
    public async Task Маршрут_идёт_от_подразделения_инициатора_к_закупкам()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var участники = await МаршрутАsync(db, стенд);

        Assert.Equal(
            new int?[]
            {
                стенд.РуководительИнициатора,
                стенд.КураторИнициатора,
                стенд.РуководительБюджета,
                стенд.КураторЗакупок,
                стенд.РуководительЗакупок,
            },
            участники.ToArray());
    }

    [Fact]
    public async Task Без_руководителя_подразделения_маршрут_не_строится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var закупки = await db.OrganizationUnits.FirstAsync(u => u.TitleRu == "Сектор закупок");
        закупки.HeadUserId = null;
        await db.SaveChangesAsync();

        // Этап без назначенного человека означал бы согласующего, которому некому
        // поставить задачу: маршрут встал бы молча.
        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => МаршрутАsync(db, стенд));

        Assert.Contains("Сектор закупок", ошибка.Message);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int RequestId, int Actor,
        int РуководительИнициатора, int КураторИнициатора,
        int РуководительБюджета,
        int РуководительЗакупок, int КураторЗакупок,
        int РуководительАдмин, int КураторАдмин);

    private static async Task<List<int?>> МаршрутАsync(DelosferaDbContext db, Стенд стенд)
    {
        var audit = new AuditService(db);
        var engine = new RouteEngine(db, audit, [], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));
        var service = new ProcurementRouteService(db, engine, new delosfera_server.Modules.Workflow.Services.RouteTemplateSelector(db));

        var request = await db.ProcurementRequests.FirstAsync(r => r.Id == стенд.RequestId);
        var instance = await service.StartAsync(request, стенд.Actor);

        return await db.RouteSteps
            .Where(s => s.RouteInstanceId == instance.Id)
            .OrderBy(s => s.Order)
            .SelectMany(s => s.Participants.Select(p => p.UserId))
            .ToListAsync();
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var автор = await ПользовательАsync(db, "Инициатор заявки");

        var (инициатор, ри, ки) = await ПодразделениеАsync(db, "Управление делами");
        var (_, рб, _) = await ПодразделениеАsync(db, "Управление планирования и анализа");
        var (_, рз, кз) = await ПодразделениеАsync(db, "Сектор закупок");
        var (_, ра, ка) = await ПодразделениеАsync(db, "Административный отдел");

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = "Draft",
            AuthorId = автор,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = document.Id,
            Subject = "Картриджи",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 30_000,
            HasBudget = true,
            MethodId = method.Id,
            InitiatorUnitId = инициатор,
            ApprovalAuthority = ApprovalAuthority.Curator,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        return new Стенд(request.Id, автор, ри, ки, рб, рз, кз, ра, ка);
    }

    private static async Task<(int UnitId, int Head, int Curator)> ПодразделениеАsync(
        DelosferaDbContext db, string titleRu)
    {
        var head = await ПользовательАsync(db, $"Руководитель: {titleRu}");
        var curator = await ПользовательАsync(db, $"Куратор: {titleRu}");

        var unit = await db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == titleRu);

        if (unit is null)
        {
            unit = new OrganizationUnit {TitleRu = titleRu};
            db.OrganizationUnits.Add(unit);
        }

        unit.HeadUserId = head;
        unit.CuratorUserId = curator;
        await db.SaveChangesAsync();

        return (unit.Id, head, curator);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"route-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
