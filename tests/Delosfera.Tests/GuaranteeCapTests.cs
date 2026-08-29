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
/// Предел гарантийного обеспечения.
///
/// По Положению банк не вправе требовать обеспечения больше двух процентов от
/// суммы закупки и пяти — от суммы договора. Проверки не было вовсе: принималась
/// любая сумма, и завышенное обеспечение выглядело обычной записью в реестре.
/// </summary>
[Collection(PostgresCollection.Name)]
public class GuaranteeCapTests
{
    private readonly PostgresFixture _postgres;

    public GuaranteeCapTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Обеспечение_заявки_в_пределах_двух_процентов_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, суммаЗакупки: 5_000_000m);

        var g = await Сервис(db).CreateAsync(ГОКЗ(стенд.TenderId, 100_000m), стенд.Actor);

        Assert.Equal(100_000m, g.Amount);
    }

    [Fact]
    public async Task Обеспечение_заявки_сверх_двух_процентов_отклоняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, суммаЗакупки: 5_000_000m);

        var ошибка = await Assert.ThrowsAsync<ArgumentException>(
            () => Сервис(db).CreateAsync(ГОКЗ(стенд.TenderId, 500_000m), стенд.Actor));

        Assert.Contains("2%", ошибка.Message);
        Assert.Contains("100 000", ошибка.Message.Replace(' ', ' '));
    }

    [Fact]
    public async Task Ровно_предел_проходит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, суммаЗакупки: 5_000_000m);

        // Граница включительна: два процента — это потолок, а не «строго меньше».
        var g = await Сервис(db).CreateAsync(ГОКЗ(стенд.TenderId, 100_000m), стенд.Actor);

        Assert.Equal(100_000m, g.Amount);
    }

    [Fact]
    public async Task Обеспечение_договора_считается_от_суммы_договора()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, суммаЗакупки: 5_000_000m, суммаДоговора: 4_800_000m);

        var g = await Сервис(db).CreateAsync(ГОИД(стенд.ContractId, 240_000m), стенд.Actor);
        Assert.Equal(240_000m, g.Amount);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Сервис(db).CreateAsync(ГОИД(стенд.ContractId, 300_000m), стенд.Actor));
    }

    [Fact]
    public async Task Доля_берётся_из_параметров_банка()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, суммаЗакупки: 5_000_000m);

        db.ProcurementParameters.Add(new ProcurementParameter
        {
            Code = "BidSecurityShare",
            Value = 0.01m,
            TitleRu = "Предельная доля ГОКЗ",
            Unit = "доля",
        });
        await db.SaveChangesAsync();

        // Банк ужесточил предел до одного процента — прежние 2% больше не проходят.
        await Assert.ThrowsAsync<ArgumentException>(
            () => Сервис(db).CreateAsync(ГОКЗ(стенд.TenderId, 100_000m), стенд.Actor));

        var g = await Сервис(db).CreateAsync(ГОКЗ(стенд.TenderId, 50_000m), стенд.Actor);
        Assert.Equal(50_000m, g.Amount);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int TenderId, int ContractId, int Actor);

    private static GuaranteeCreateRequest ГОКЗ(int tenderId, decimal amount) => new()
    {
        Kind = GuaranteeKind.BidSecurity,
        TenderId = tenderId,
        SupplierTitle = "ОсОО Проба",
        SupplierInn = "01234567890123",
        Amount = amount,
        ReceivedOn = new DateOnly(2026, 9, 1),
        ValidUntil = new DateOnly(2026, 12, 1),
    };

    private static GuaranteeCreateRequest ГОИД(int contractId, decimal amount) => new()
    {
        Kind = GuaranteeKind.PerformanceSecurity,
        ContractId = contractId,
        SupplierTitle = "ОсОО Проба",
        SupplierInn = "01234567890123",
        Amount = amount,
        ReceivedOn = new DateOnly(2026, 9, 25),
        ValidUntil = new DateOnly(2027, 1, 25),
    };

    private static IGuaranteeService Сервис(DelosferaDbContext db) =>
        new GuaranteeService(db, new AuditService(db), new delosfera_server.Common.Services.BankClock());

    private static async Task<Стенд> SeedAsync(
        DelosferaDbContext db, decimal суммаЗакупки, decimal? суммаДоговора = null)
    {
        var автор = new User
        {
            FullName = "Инициатор",
            Email = $"guar-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(автор);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка",
            StatusCode = "Draft",
            AuthorId = автор.Id,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Оборудование",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = суммаЗакупки,
            MethodId = method.Id,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var tender = new Tender {RequestId = request.Id, Status = TenderStatus.Published};
        db.Tenders.Add(tender);

        var contractDoc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Договор",
            StatusCode = "Active",
            AuthorId = автор.Id,
        };
        db.Documents.Add(contractDoc);
        await db.SaveChangesAsync();

        var поставщик = new Supplier {Title = "ОсОО Проба", Inn = "01234567890123"};
        db.Suppliers.Add(поставщик);
        await db.SaveChangesAsync();

        var contract = new ProcurementContract
        {
            DocumentId = contractDoc.Id,
            RequestId = request.Id,
            SupplierId = поставщик.Id,
            Amount = суммаДоговора ?? 0m,
        };
        db.ProcurementContracts.Add(contract);
        await db.SaveChangesAsync();

        return new Стенд(tender.Id, contract.Id, автор.Id);
    }
}
