using System.Security.Claims;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.RiskEvents.DTO;
using delosfera_server.Modules.RiskEvents.Models;
using delosfera_server.Modules.RiskEvents.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Delosfera.Tests;

/// <summary>
/// События операционного риска (RISK-1, 716-П / НБКР).
///
/// Событие поднимается с шага согласования: система должна сама привязать его к
/// документу и маршруту по одной задаче участника — иначе риск-подразделение
/// получит «сбой процесса» без указания места. Реестр фильтруют по типу и статусу.
/// Доступ к контуру закрыт правом: без ManageRiskEvents поднять событие нельзя.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RiskEventTests
{
    private readonly PostgresFixture _postgres;

    public RiskEventTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Поднятое_с_шага_событие_привязано_к_документу_и_маршруту()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, instanceId, stepId, participantId) = await SeedRouteAsync(db);
        TestSupport.EnsureUser(db, ИнициаторId);

        var service = Service(db);

        var dto = await service.RaiseAsync(new RaiseRiskEventRequest
        {
            Type = RiskEventType.ExecutionDeliveryProcessManagement,
            Severity = RiskEventSeverity.High,
            Description = "Просрочен этап согласования сверх норматива",
            RouteParticipantId = participantId,
            DirectLossAmount = 15000m,
            LossCurrency = "kgs",
        }, ИнициаторId);

        // Из одной задачи участника выведены этап, маршрут и документ.
        Assert.Equal(documentId, dto.DocumentId);
        Assert.Equal(instanceId, dto.RouteInstanceId);
        Assert.Equal(stepId, dto.RouteStepId);
        Assert.Equal(participantId, dto.RouteParticipantId);
        Assert.Equal(nameof(RiskEventStatus.New), dto.Status);
        Assert.Equal(15000m, dto.DirectLossAmount);
        Assert.Equal("KGS", dto.LossCurrency); // валюта нормализована в верхний регистр
        Assert.Equal(ИнициаторId, dto.InitiatorUserId);
    }

    [Fact]
    public async Task Без_описания_событие_не_поднять()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ИнициаторId);

        var service = Service(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RaiseAsync(new RaiseRiskEventRequest
            {
                Type = RiskEventType.InternalFraud,
                Description = "   ",
            }, ИнициаторId));

        Assert.False(db.RiskEvents.Any());
    }

    [Fact]
    public async Task Реестр_фильтрует_по_статусу_и_типу()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ИнициаторId);
        var service = Service(db);

        var внутреннее = await service.RaiseAsync(new RaiseRiskEventRequest
        {
            Type = RiskEventType.InternalFraud,
            Description = "Обход принципа «четырёх глаз» на подписании",
        }, ИнициаторId);

        await service.RaiseAsync(new RaiseRiskEventRequest
        {
            Type = RiskEventType.ExternalFraud,
            Description = "Внешняя попытка подлога документа",
        }, ИнициаторId);

        // Переводим первое в работу — фильтр по статусу должен вернуть только его.
        await service.ChangeStatusAsync(внутреннее.Id,
            new ChangeRiskEventStatusRequest { Status = RiskEventStatus.InProgress }, ИнициаторId);

        var поСтатусу = await service.SearchAsync(new RiskEventFilter { Status = RiskEventStatus.InProgress });
        Assert.Single(поСтатусу.Items);
        Assert.Equal(внутреннее.Id, поСтатусу.Items[0].Id);

        var поТипу = await service.SearchAsync(new RiskEventFilter { Type = RiskEventType.ExternalFraud });
        Assert.Single(поТипу.Items);
        Assert.Equal(nameof(RiskEventType.ExternalFraud), поТипу.Items[0].Type);

        // Полный реестр — оба события.
        var всё = await service.SearchAsync(new RiskEventFilter());
        Assert.Equal(2, всё.Total);
    }

    [Fact]
    public async Task Смена_статуса_фиксирует_прямые_потери_и_комментарий()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ИнициаторId);
        var service = Service(db);

        var событие = await service.RaiseAsync(new RaiseRiskEventRequest
        {
            Type = RiskEventType.ExecutionDeliveryProcessManagement,
            Description = "Срыв контрольной процедуры",
        }, ИнициаторId);

        var закрыто = await service.ChangeStatusAsync(событие.Id, new ChangeRiskEventStatusRequest
        {
            Status = RiskEventStatus.Closed,
            Comment = "Устранено, потери возмещены",
            DirectLossAmount = 2500m,
            LossCurrency = "USD",
        }, ИнициаторId);

        Assert.Equal(nameof(RiskEventStatus.Closed), закрыто.Status);
        Assert.Equal(2500m, закрыто.DirectLossAmount);
        Assert.Equal("USD", закрыто.LossCurrency);
        Assert.Equal("Устранено, потери возмещены", закрыто.ResolutionComment);
    }

    [Fact]
    public void Без_права_ManageRiskEvents_поднятие_запрещено_403()
    {
        // Вошедший сотрудник без права ManageRiskEvents.
        var context = ФильтрКонтекст(new Claim("permission", ((int)PermissionCode.ViewRiskEvents).ToString()));

        new RequirePermissionAttribute(PermissionCode.ManageRiskEvents).OnAuthorization(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void С_правом_ManageRiskEvents_поднятие_разрешено()
    {
        var context = ФильтрКонтекст(new Claim("permission", ((int)PermissionCode.ManageRiskEvents).ToString()));

        new RequirePermissionAttribute(PermissionCode.ManageRiskEvents).OnAuthorization(context);

        Assert.Null(context.Result); // право есть — фильтр запрос не перехватывает
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private const int ИнициаторId = 7701;

    private static RiskEventService Service(DelosferaDbContext db) =>
        new(db, new AuditService(db), new BankClock());

    private static AuthorizationFilterContext ФильтрКонтекст(params Claim[] claims)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")),
        };

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static async Task<(int DocumentId, int InstanceId, int StepId, int ParticipantId)> SeedRouteAsync(
        DelosferaDbContext db)
    {
        var author = new User
        {
            FullName = "Автор документа",
            Email = $"author-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Документ для проверки оп-риска",
            StatusCode = "OnApproval",
            AuthorId = author.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var participant = new RouteParticipant
        {
            UserId = author.Id,
            State = ParticipantState.Active,
            ActivatedAt = DateTime.UtcNow.AddHours(-5),
            DueAt = DateTime.UtcNow.AddHours(-1),
            Required = true,
        };

        var instance = new RouteInstance
        {
            DocumentId = document.Id,
            Status = RouteInstanceStatus.Running,
            CurrentStepOrder = 1,
            StartedAt = DateTime.UtcNow.AddHours(-5),
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Approval,
                    ActivatedAt = DateTime.UtcNow.AddHours(-5),
                    Participants = [participant],
                },
            ],
        };
        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();

        return (document.Id, instance.Id, instance.Steps.First().Id, participant.Id);
    }
}
