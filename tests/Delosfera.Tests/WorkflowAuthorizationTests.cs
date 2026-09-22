using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Controllers;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Авторизация движка согласования (Workflow): маршрут по документу заводит,
/// запускает и снимает по нему замечания только автор документа либо администратор
/// (ManageSystemSettings). До правки любой аутентифицированный пользователь мог
/// запустить чужой черновик маршрута, закрыть чужое замечание или создать маршрут
/// по чужому документу. Проверка стоит в контроллере — публичной точке входа;
/// внутренние контурные вызовы (СЗ/закупки/кастом-документы) идут в движок напрямую
/// и ей не затрагиваются.
/// </summary>
[Collection(PostgresCollection.Name)]
public class WorkflowAuthorizationTests
{
    private readonly PostgresFixture _postgres;

    public WorkflowAuthorizationTests(PostgresFixture postgres) => _postgres = postgres;

    // ── instances/from-template ────────────────────────────────────────────────

    [Fact]
    public async Task Instantiate_ByAuthor_IsAllowed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId));

        await controller.Instantiate(new InstantiateRequest { DocumentId = documentId, TemplateId = 1 });

        Assert.True(engine.InstantiateCalled);
    }

    [Fact]
    public async Task Instantiate_ByAdmin_IsAllowed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var engine = new RecordingEngine();
        var controller = NewController(
            db, engine, new FakeCurrentUser(authorId + 1000, PermissionCode.ManageSystemSettings));

        await controller.Instantiate(new InstantiateRequest { DocumentId = documentId, TemplateId = 1 });

        Assert.True(engine.InstantiateCalled);
    }

    [Fact]
    public async Task Instantiate_ByStranger_IsBlocked()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId + 1000));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.Instantiate(new InstantiateRequest { DocumentId = documentId, TemplateId = 1 }));

        Assert.False(engine.InstantiateCalled);
    }

    // ── instances/{id}/start ───────────────────────────────────────────────────

    [Fact]
    public async Task Start_ByAuthor_IsAllowed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var instanceId = await SeedDraftInstanceAsync(db, documentId);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId));

        await controller.Start(instanceId);

        Assert.True(engine.StartCalled);
    }

    [Fact]
    public async Task Start_ByAdmin_IsAllowed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var instanceId = await SeedDraftInstanceAsync(db, documentId);
        var engine = new RecordingEngine();
        var controller = NewController(
            db, engine, new FakeCurrentUser(authorId + 1000, PermissionCode.ManageSystemSettings));

        await controller.Start(instanceId);

        Assert.True(engine.StartCalled);
    }

    [Fact]
    public async Task Start_ByStranger_IsBlocked()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var instanceId = await SeedDraftInstanceAsync(db, documentId);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId + 1000));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Start(instanceId));

        Assert.False(engine.StartCalled);
    }

    // ── remarks/{id}/confirm ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmRemark_ByAuthor_IsAllowed()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var remarkId = await SeedRemarkAsync(db, documentId);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId));

        await controller.ConfirmRemark(remarkId);

        Assert.True(engine.ConfirmRemarkCalled);
    }

    [Fact]
    public async Task ConfirmRemark_ByStranger_IsBlocked()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDocumentAsync(db);
        var remarkId = await SeedRemarkAsync(db, documentId);
        var engine = new RecordingEngine();
        var controller = NewController(db, engine, new FakeCurrentUser(authorId + 1000));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.ConfirmRemark(remarkId));

        Assert.False(engine.ConfirmRemarkCalled);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static WorkflowController NewController(
        DelosferaDbContext db, IRouteEngine engine,
        delosfera_server.Common.Services.Authorization.ICurrentUserService currentUser)
    {
        // Вошедший пользователь: без этого UnauthorizedAccessException средой
        // трактовался бы как 401, а не 403. В контроллерных путях авторизации
        // задач inbox/делегирования нет — их зависимости не задействованы.
        var controller = new WorkflowController(
            db, engine, currentUser, inbox: null!, new AuditService(db), delegation: null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity("test")),
                },
            },
        };
        return controller;
    }

    private static async Task<(int DocumentId, int AuthorId)> SeedDocumentAsync(DelosferaDbContext db)
    {
        var author = new User
        {
            FullName = "Автор документа",
            Email = $"wf-author-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = "Документ для проверки авторизации маршрута",
            StatusCode = "Draft",
            AuthorId = author.Id,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (document.Id, author.Id);
    }

    private static async Task<int> SeedDraftInstanceAsync(DelosferaDbContext db, int documentId)
    {
        var instance = new RouteInstance
        {
            DocumentId = documentId,
            Status = RouteInstanceStatus.Draft,
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Approval,
                    Participants = [new RouteParticipant { UserId = 1, Required = true, State = ParticipantState.Pending }],
                },
            ],
        };
        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }

    private static async Task<int> SeedRemarkAsync(DelosferaDbContext db, int documentId)
    {
        var participant = new RouteParticipant { UserId = 1, State = ParticipantState.Done, Required = true };
        var instance = new RouteInstance
        {
            DocumentId = documentId,
            Status = RouteInstanceStatus.OnRevision,
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Approval,
                    Participants = [participant],
                },
            ],
        };
        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();

        var resolution = new Resolution
        {
            RouteParticipantId = participant.Id,
            Type = ResolutionType.ApprovedWithRemarks,
            Comment = "Доработать",
            At = DateTime.UtcNow,
        };
        db.Resolutions.Add(resolution);
        await db.SaveChangesAsync();

        var remark = new Remark { ResolutionId = resolution.Id, Text = "Доработать", State = RemarkState.Open };
        db.Remarks.Add(remark);
        await db.SaveChangesAsync();

        return remark.Id;
    }

    /// <summary>
    /// Движок-заглушка: фиксирует, дошёл ли вызов до него. Тесты проверяют именно
    /// решение об авторизации в контроллере, а не поведение движка — при блокировке
    /// его метод вызываться не должен.
    /// </summary>
    private sealed class RecordingEngine : IRouteEngine
    {
        public bool InstantiateCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool ConfirmRemarkCalled { get; private set; }

        public Task<RouteInstance> InstantiateFromTemplateAsync(
            int documentId, int templateId, IReadOnlySet<string>? satisfiedConditions = null)
        {
            InstantiateCalled = true;
            return Task.FromResult(new RouteInstance { Id = 0, DocumentId = documentId });
        }

        public Task StartAsync(int routeInstanceId, int actorUserId)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task ConfirmRemarkResolvedAsync(int remarkId, int actorUserId)
        {
            ConfirmRemarkCalled = true;
            return Task.CompletedTask;
        }

        public Task ResolveAsync(int participantId, ResolutionType type, string? comment, int actorUserId, int? signatureId = null) => throw new NotSupportedException();
        public Task AppendSigningStepAsync(int routeInstanceId, int signerUserId) => throw new NotSupportedException();
        public Task<RouteInstance> InstantiateForSignerAsync(int documentId, int signerUserId, int? timeNormHours = null) => throw new NotSupportedException();
        public Task<RouteInstance> InstantiateForApproversAsync(int documentId, IReadOnlyList<int> approverUserIds, bool parallel, int? timeNormHours = null, int? signerUserId = null) => throw new NotSupportedException();
        public Task ApplyOverdueAsync(DateTime now) => throw new NotSupportedException();
        public Task InterruptAsync(int routeInstanceId, int actorUserId) => throw new NotSupportedException();
    }
}
