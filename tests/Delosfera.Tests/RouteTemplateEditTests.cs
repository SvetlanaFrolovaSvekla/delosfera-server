using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Controllers;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Настройка маршрута: кто согласует и в каком порядке.
///
/// Шаблон можно было завести и прочитать, но не изменить — а маршрут меняется
/// вместе с процессом. Правится он целиком: маршрут это порядок, и правка одного
/// этапа в отрыве от соседних чаще ломает последовательность, чем чинит.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RouteTemplateEditTests
{
    private readonly PostgresFixture _postgres;

    public RouteTemplateEditTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Маршрут_переписывается_целиком()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 2);

        await Контроллер(db).UpdateTemplate(шаблон, Запрос(
            Этап(1, RouteRoles.AuthorHead),
            Этап(2, RouteRoles.AuthorCurator),
            Этап(3, RouteRoles.BoardChairman)), default);

        var шаги = await ШагиАsync(db, шаблон);

        Assert.Equal(3, шаги.Count);
        Assert.Equal([1, 2, 3], шаги.Select(s => s.Order));
        Assert.Equal(RouteRoles.BoardChairman, шаги[2].Participants.Single().RoleRef);
    }

    [Fact]
    public async Task Порядок_этапов_нумеруется_заново()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        // Пришли с разрывами в нумерации — сохраняем подряд.
        await Контроллер(db).UpdateTemplate(шаблон, Запрос(
            Этап(10, RouteRoles.AuthorHead),
            Этап(20, RouteRoles.BoardChairman)), default);

        Assert.Equal([1, 2], (await ШагиАsync(db, шаблон)).Select(s => s.Order));
    }

    [Fact]
    public async Task Этап_без_согласующих_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        // Маршрут остановился бы на таком этапе навсегда.
        var ответ = await Контроллер(db).UpdateTemplate(шаблон, new CreateRouteTemplateRequest
        {
            Name = "Пустой этап",
            DocumentType = DocumentType.Sz,
            Steps = [new TemplateStepDto {Order = 1}],
        }, default);

        var плохой = Assert.IsType<BadRequestObjectResult>(ответ);
        Assert.Contains("нет согласующих", плохой.Value!.ToString());
    }

    [Fact]
    public async Task Маршрут_без_этапов_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        var ответ = await Контроллер(db).UpdateTemplate(шаблон, new CreateRouteTemplateRequest
        {
            Name = "Без этапов", DocumentType = DocumentType.Sz, Steps = [],
        }, default);

        Assert.IsType<BadRequestObjectResult>(ответ);
    }

    [Fact]
    public async Task Шаблон_закреплённый_за_видом_записки_не_удаляется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        var kind = await db.SzKinds.FirstAsync();
        kind.RouteTemplateId = шаблон;
        await db.SaveChangesAsync();

        // Иначе вид записки остался бы без маршрута, и отправить её стало бы нечем.
        var ответ = await Контроллер(db).DeleteTemplate(шаблон, default);

        var плохой = Assert.IsType<BadRequestObjectResult>(ответ);
        Assert.Contains("закреплён за видами записок", плохой.Value!.ToString());
    }

    [Fact]
    public async Task Свободный_шаблон_удаляется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        await Контроллер(db).DeleteTemplate(шаблон, default);

        Assert.Empty(await db.RouteTemplates.Where(t => t.Id == шаблон).ToListAsync());
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static WorkflowController Контроллер(DelosferaDbContext db)
    {
        var audit = new AuditService(db);

        return new WorkflowController(
            db,
            new RouteEngine(db, audit, [], new NoSubstitutions(), new SilentNotifier(),
                new FakeSignatures(), new RouteRoleResolver(db)),
            new FakeCurrentUser(1, PermissionCode.ManageSystemSettings),
            new TaskInboxService(db, new NoSubstitutions(), new BankClock()),
            audit);
    }

    private static CreateRouteTemplateRequest Запрос(params TemplateStepDto[] steps) =>
        new()
        {
            Name = "Кадровая записка: приём",
            DocumentType = DocumentType.Sz,
            Steps = steps.ToList(),
        };

    private static TemplateStepDto Этап(int order, string roleRef) => new()
    {
        Order = order,
        Participants = [new TemplateParticipantDto {RoleRef = roleRef, Required = true}],
    };

    private static async Task<List<RouteTemplateStep>> ШагиАsync(DelosferaDbContext db, int templateId) =>
        await db.RouteTemplateSteps.AsNoTracking()
            .Include(s => s.Participants)
            .Where(s => s.RouteTemplateId == templateId)
            .OrderBy(s => s.Order)
            .ToListAsync();

    private static async Task<int> ШаблонАsync(DelosferaDbContext db, int этапов)
    {
        var template = new RouteTemplate
        {
            DocumentType = DocumentType.Sz,
            Name = "Исходный маршрут",
            Steps = Enumerable.Range(1, этапов).Select(i => new RouteTemplateStep
            {
                Order = i,
                Participants = [new RouteTemplateParticipant {RoleRef = RouteRoles.AuthorHead}],
            }).ToList(),
        };

        db.RouteTemplates.Add(template);
        await db.SaveChangesAsync();

        return template.Id;
    }
}
