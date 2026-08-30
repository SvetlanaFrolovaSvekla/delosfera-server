using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Путь заявки на закупку от согласования до исполнения.
///
/// Обработчика завершения маршрута у этого контура не было вовсе: заявка
/// проходила все визы и навсегда оставалась «на согласовании». Статусы
/// «в закупке» и «завершена» в коде только читались счётчиками — выставить их
/// было некому, и там всегда стояли нули.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProcurementLifecycleTests
{
    private readonly PostgresFixture _postgres;

    public ProcurementLifecycleTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Пройденный_маршрут_переводит_заявку_в_закупку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Обработчик(db).OnRouteApprovedAsync(стенд.RouteId, стенд.DocumentId, стенд.Actor);

        Assert.Equal(ProcurementStatus.InProcurement, await СтатусАsync(db, стенд.DocumentId));
    }

    [Fact]
    public async Task Отклонённый_маршрут_отклоняет_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Обработчик(db).OnRouteStatusChangedAsync(
            стенд.RouteId, стенд.DocumentId, RouteInstanceStatus.Rejected, стенд.Actor);

        Assert.Equal(ProcurementStatus.Rejected, await СтатусАsync(db, стенд.DocumentId));
    }

    [Fact]
    public async Task Возврат_на_доработку_виден_в_статусе()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        await Обработчик(db).OnRouteStatusChangedAsync(
            стенд.RouteId, стенд.DocumentId, RouteInstanceStatus.OnRevision, стенд.Actor);

        Assert.Equal(ProcurementStatus.OnRevision, await СтатусАsync(db, стенд.DocumentId));
    }

    [Fact]
    public async Task Чужой_документ_обработчик_не_трогает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var чужой = new Document
        {
            Type = DocumentType.Sz,
            Title = "Служебная записка",
            StatusCode = "OnApproval",
            AuthorId = стенд.Actor,
        };
        db.Documents.Add(чужой);
        await db.SaveChangesAsync();

        // Обработчики контуров висят на одном событии и обязаны узнавать свои
        // документы: иначе записка получила бы статус заявки на закупку.
        await Обработчик(db).OnRouteApprovedAsync(стенд.RouteId, чужой.Id, стенд.Actor);

        Assert.Equal("OnApproval", await СтатусАsync(db, чужой.Id));
    }

    [Fact]
    public async Task Закрытый_договор_завершает_и_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var договоры = Договоры(db);

        var contract = new ProcurementContract
        {
            DocumentId = стенд.ContractDocumentId,
            RequestId = стенд.RequestId,
            SupplierId = стенд.SupplierId,
            Amount = 300_000m,
            InitialAmount = 300_000m,
            Status = ContractStatus.Active,
        };
        db.ProcurementContracts.Add(contract);
        await db.SaveChangesAsync();

        await договоры.AddActAsync(contract.Id,
            new DeliveryActRequest {Number = "АКТ-1", Amount = 300_000m}, стенд.Actor);

        var act = await db.DeliveryActs.FirstAsync(a => a.ContractId == contract.Id);
        await договоры.ApproveActAsync(act.Id, asCurator: false, стенд.Actor);

        // Обязательство исполнено — закрывается не только договор, но и закупка,
        // с которой всё началось.
        Assert.Equal(ProcurementStatus.Completed, await СтатусАsync(db, стенд.DocumentId));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int RequestId, int DocumentId, int ContractDocumentId, int RouteId, int SupplierId, int Actor);

    private static IRouteCompletionHandler Обработчик(DelosferaDbContext db)
    {
        var audit = new AuditService(db);

        return new ProcurementRouteCompletionHandler(
            db, new DocumentService(db, audit, new NumeratorService(db)), audit);
    }

    private static IContractService Договоры(DelosferaDbContext db)
    {
        var audit = new AuditService(db);

        return new ContractService(db,
            new DocumentService(db, audit, new NumeratorService(db)), audit, new BankClock());
    }

    private static async Task<string> СтатусАsync(DelosferaDbContext db, int documentId) =>
        await db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.StatusCode)
            .FirstAsync();

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var автор = new User
        {
            FullName = "Инициатор закупки",
            Email = $"life-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(автор);

        var поставщик = new Supplier {Title = "ОсОО Поставщик", Inn = "01234567890123"};
        db.Suppliers.Add(поставщик);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = ProcurementStatus.OnApproval,
            AuthorId = автор.Id,
        };
        var contractDoc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Договор",
            StatusCode = "Active",
            AuthorId = автор.Id,
        };
        db.Documents.AddRange(doc, contractDoc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Ноутбуки",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 300_000m,
            MethodId = method.Id,
        };
        db.ProcurementRequests.Add(request);

        var route = new RouteInstance {DocumentId = doc.Id, Status = RouteInstanceStatus.Running};
        db.RouteInstances.Add(route);
        await db.SaveChangesAsync();

        return new Стенд(request.Id, doc.Id, contractDoc.Id, route.Id, поставщик.Id, автор.Id);
    }
}
