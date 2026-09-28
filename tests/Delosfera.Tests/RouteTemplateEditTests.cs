using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
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
    public async Task Параллельный_и_условный_этапы_сохраняются_и_читаются()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        // Конструктор графа отдаёт этап-развилку (все согласуют сразу) и условный
        // этап (вынесение на орган по порогу). Проверяем, что режим, условие и вид
        // этапа переживают запись целиком и читаются обратно.
        await Контроллер(db).UpdateTemplate(шаблон, new CreateRouteTemplateRequest
        {
            Name = "Смешанный маршрут",
            DocumentType = DocumentType.Sz,
            Steps =
            [
                new TemplateStepDto
                {
                    Order = 1, Mode = StepMode.Parallel, Kind = StepKind.Approval,
                    Participants =
                    [
                        new TemplateParticipantDto {RoleRef = RouteRoles.AuthorHead, Required = true},
                        new TemplateParticipantDto {RoleRef = RouteRoles.AuthorCurator, Required = true},
                    ],
                },
                new TemplateStepDto
                {
                    Order = 2, Mode = StepMode.Sequential, Kind = StepKind.Board,
                    Condition = "board-authority",
                    Participants = [new TemplateParticipantDto {RoleRef = RouteRoles.BoardChairman, Required = true}],
                },
            ],
        }, default);

        var шаги = await ШагиАsync(db, шаблон);

        Assert.Equal(2, шаги.Count);
        Assert.Equal(StepMode.Parallel, шаги[0].Mode);
        Assert.Equal(2, шаги[0].Participants.Count);
        Assert.Null(шаги[0].Condition);
        Assert.Equal(StepMode.Sequential, шаги[1].Mode);
        Assert.Equal(StepKind.Board, шаги[1].Kind);
        Assert.Equal("board-authority", шаги[1].Condition);
    }

    [Fact]
    public void Каталог_ролей_содержит_кадровика_с_названием()
    {
        // Список ролей конструктора задаётся на сервере (RouteRoles.Title), а не в клиенте.
        var роли = RouteRoles.Catalog().ToList();

        Assert.Contains(роли, r => r.Value == RouteRoles.HrOfficer && r.Title == "Кадровик УЧР");
        Assert.Contains(роли, r => r.Value == RouteRoles.BoardChairman);
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

    [Fact]
    public async Task Финальный_методолог_не_последний_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        // Этап финального контроля стоит первым — движок такой маршрут не запустит.
        var ответ = await Контроллер(db).UpdateTemplate(шаблон, new CreateRouteTemplateRequest
        {
            Name = "Кадровая",
            DocumentType = DocumentType.Sz,
            Steps =
            [
                new TemplateStepDto
                {
                    Order = 1, Kind = StepKind.FinalControl, IsFinalMethodology = true,
                    Participants = [new TemplateParticipantDto {RoleRef = RouteRoles.AuthorHead, Required = true}],
                },
                new TemplateStepDto
                {
                    Order = 2, Kind = StepKind.Approval,
                    Participants = [new TemplateParticipantDto {RoleRef = RouteRoles.BoardChairman, Required = true}],
                },
            ],
        }, default);

        var плохой = Assert.IsType<BadRequestObjectResult>(ответ);
        Assert.Contains("финального контроля методологии должен быть последним", плохой.Value!.ToString());
    }

    [Fact]
    public async Task Параллельная_развилка_без_обязательного_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        var ответ = await Контроллер(db).UpdateTemplate(шаблон, new CreateRouteTemplateRequest
        {
            Name = "Развилка",
            DocumentType = DocumentType.Sz,
            Steps =
            [
                new TemplateStepDto
                {
                    Order = 1, Mode = StepMode.Parallel, Kind = StepKind.Approval,
                    Participants =
                    [
                        new TemplateParticipantDto {RoleRef = RouteRoles.AuthorHead, Required = false},
                        new TemplateParticipantDto {RoleRef = RouteRoles.AuthorCurator, Required = false},
                    ],
                },
            ],
        }, default);

        var плохой = Assert.IsType<BadRequestObjectResult>(ответ);
        Assert.Contains("развилка без обязательного", плохой.Value!.ToString());
    }

    [Fact]
    public async Task Поля_этапа_срок_подпись_методолог_переживают_запись()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var шаблон = await ШаблонАsync(db, этапов: 1);

        await Контроллер(db).UpdateTemplate(шаблон, Запрос(new TemplateStepDto
        {
            Order = 1,
            Kind = StepKind.FinalControl,
            IsFinalMethodology = true,
            TimeNormHours = 24,
            RequiredSignatureLevel = delosfera_server.Modules.Signing.Models.SignatureLevel.Qualified,
            Participants = [new TemplateParticipantDto {RoleRef = RouteRoles.AuthorHead, Required = true}],
        }), default);

        var шаг = (await ШагиАsync(db, шаблон)).Single();
        Assert.True(шаг.IsFinalMethodology);
        Assert.Equal(24, шаг.TimeNormHours);
        Assert.Equal(delosfera_server.Modules.Signing.Models.SignatureLevel.Qualified, шаг.RequiredSignatureLevel);
    }

    [Fact]
    public async Task Условия_приходят_по_виду_документа()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // У закупки условные этапы есть, у СЗ — нет.
        var закупка = Assert.IsType<OkObjectResult>(Контроллер(db).ListRouteConditions("Procurement"));
        var записка = Assert.IsType<OkObjectResult>(Контроллер(db).ListRouteConditions("Sz"));

        Assert.NotEmpty((System.Collections.IEnumerable) закупка.Value!);
        Assert.Empty((System.Collections.IEnumerable) записка.Value!);
    }

    [Fact]
    public async Task Предпросмотр_роли_разворачивает_руководителя_подразделения()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var head = new User {FullName = "Иванов И.И.", Email = $"h-{Guid.NewGuid():N}@k.kg", PasswordHash = "x"};
        db.Users.Add(head);
        await db.SaveChangesAsync();
        var unit = new OrganizationUnit {TitleRu = "Управление кредитования", HeadUserId = head.Id};
        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();

        var ответ = Assert.IsType<OkObjectResult>(await Контроллер(db).PreviewRoles(new RolePreviewRequest
        {
            OrgUnitId = unit.Id,
            Roles = [RouteRoles.AuthorHead, RouteRoles.TargetUnitHead],
        }, default));

        var items = Assert.IsType<List<RolePreviewItemResponse>>(ответ.Value);
        var authorHead = items.Single(i => i.RoleRef == RouteRoles.AuthorHead);
        Assert.True(authorHead.Resolved);
        Assert.Equal(head.Id, authorHead.UserId);

        // Роль «из документа» без пробного подразделения не разрешается — это не ошибка справочника.
        var targetHead = items.Single(i => i.RoleRef == RouteRoles.TargetUnitHead);
        Assert.False(targetHead.Resolved);
        Assert.Contains("карточки документа", targetHead.Note);
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
            audit, new delosfera_server.Modules.Workflow.Services.TaskDelegationService(db, new NoSubstitutions(), audit),
            new RouteRoleResolver(db));
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
