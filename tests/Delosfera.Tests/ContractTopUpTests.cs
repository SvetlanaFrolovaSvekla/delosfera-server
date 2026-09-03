using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Дополнительное количество по договору — не более четверти его стоимости.
///
/// Право дано Положением для договоров, заключённых по результатам конкурса, и
/// только по служебной записке, согласованной с куратором инициатора,
/// организатором и куратором организатора.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ContractTopUpTests
{
    private readonly PostgresFixture _postgres;

    public ContractTopUpTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Допоставка_в_пределах_четверти_проходит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m);

        var договор = await Сервис(db).TopUpAsync(
            стенд.ContractId, new ContractTopUpRequest {Amount = 1_000_000m, SzId = стенд.SzId}, стенд.Actor);

        Assert.Equal(5_000_000m, договор.Amount);
        Assert.Equal(4_000_000m, договор.InitialAmount);
        Assert.Equal(0m, договор.TopUpAvailable);
    }

    [Fact]
    public async Task Сверх_четверти_отклоняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m);

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Сервис(db).TopUpAsync(
                стенд.ContractId, new ContractTopUpRequest {Amount = 1_000_001m, SzId = стенд.SzId}, стенд.Actor));

        Assert.Contains("25%", ошибка.Message);
    }

    [Fact]
    public async Task Вторая_допоставка_считается_от_исходной_суммы()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m);
        var сервис = Сервис(db);

        await сервис.TopUpAsync(
            стенд.ContractId, new ContractTopUpRequest {Amount = 600_000m, SzId = стенд.SzId}, стенд.Actor);

        // Иначе каждая допоставка расширяла бы право на следующую, и четверть
        // превратилась бы в бесконечный ряд: 4 млн → 4,6 → 5,75 → …
        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => сервис.TopUpAsync(
                стенд.ContractId, new ContractTopUpRequest {Amount = 500_000m, SzId = стенд.SzId}, стенд.Actor));

        Assert.Contains("уже приобретено", ошибка.Message);
    }

    [Fact]
    public async Task Без_служебной_записки_допоставки_нет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Сервис(db).TopUpAsync(
                стенд.ContractId, new ContractTopUpRequest {Amount = 100_000m}, стенд.Actor));
    }

    [Fact]
    public async Task По_договору_не_из_конкурса_права_нет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m, сКонкурсом: false);

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Сервис(db).TopUpAsync(
                стенд.ContractId, new ContractTopUpRequest {Amount = 100_000m, SzId = стенд.SzId}, стенд.Actor));

        Assert.Contains("по результатам конкурса", ошибка.Message);
    }

    [Fact]
    public async Task Расторгнутый_договор_не_дополняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, сумма: 4_000_000m);

        var договор = await db.ProcurementContracts.FirstAsync(c => c.Id == стенд.ContractId);
        договор.Status = ContractStatus.Terminated;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Сервис(db).TopUpAsync(
                стенд.ContractId, new ContractTopUpRequest {Amount = 100_000m, SzId = стенд.SzId}, стенд.Actor));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int ContractId, int SzId, int Actor);

    private static IContractService Сервис(DelosferaDbContext db)
    {
        var audit = new AuditService(db);

        return new ContractService(db,
            new DocumentService(db, audit, new NumeratorService(db)),
            audit, new BankClock());
    }

    private static async Task<Стенд> SeedAsync(
        DelosferaDbContext db, decimal сумма, bool сКонкурсом = true)
    {
        var автор = new User
        {
            FullName = "Организатор закупки",
            Email = $"topup-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(автор);

        var поставщик = new Supplier {Title = "ОсОО Поставщик", Inn = "01234567890123"};
        db.Suppliers.Add(поставщик);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var reqDoc = new Document
        {
            Type = DocumentType.Procurement, Title = "Заявка",
            StatusCode = "OnApproval", AuthorId = автор.Id,
        };
        var contractDoc = new Document
        {
            Type = DocumentType.Procurement, Title = "Договор",
            StatusCode = "Active", AuthorId = автор.Id,
        };
        db.Documents.AddRange(reqDoc, contractDoc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = reqDoc.Id,
            Subject = "Оборудование",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = сумма,
            MethodId = method.Id,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        int? tenderId = null;

        if (сКонкурсом)
        {
            var tender = new Tender {RequestId = request.Id, Status = TenderStatus.Decided};
            db.Tenders.Add(tender);
            await db.SaveChangesAsync();
            tenderId = tender.Id;
        }

        var contract = new ProcurementContract
        {
            DocumentId = contractDoc.Id,
            RequestId = request.Id,
            TenderId = tenderId,
            SupplierId = поставщик.Id,
            Amount = сумма,
            InitialAmount = сумма,
            Status = ContractStatus.Active,
        };
        db.ProcurementContracts.Add(contract);
        await db.SaveChangesAsync();

        return new Стенд(contract.Id, reqDoc.Id, автор.Id);
    }
}
