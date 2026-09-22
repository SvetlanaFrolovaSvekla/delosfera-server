using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Подписание протокола закупки: за каждую сторону подписывает только закреплённый
/// за ней сотрудник (PRC-10). Роль берётся не из тела запроса на веру — подписант
/// сверяется с оргструктурой закупки, иначе три подписи (и утверждение протокола,
/// служащее основанием для договора) можно было бы подделать, назвав чужую роль.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProtocolSignAuthorizationTests
{
    private readonly PostgresFixture _postgres;

    public ProtocolSignAuthorizationTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Закреплённый_подписант_подписывает_свою_роль()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedSignableAsync(db);
        var сервис = Сервис(db);

        var протокол = await сервис.SignAsync(
            стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.Initiator}, стенд.Initiator);

        Assert.Contains(протокол.Signatures, s => s.Role == ProtocolSignerRole.Initiator && !s.Revoked);
    }

    [Fact]
    public async Task Чужой_за_инициатора_получает_отказ()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedSignableAsync(db);
        var сервис = Сервис(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.SignAsync(
                стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.Initiator}, стенд.Stranger));

        // Подделанной подписи в протоколе не осталось.
        var протокол = await сервис.GetAsync(стенд.RequestId);
        Assert.DoesNotContain(протокол!.Signatures, s => s.Role == ProtocolSignerRole.Initiator);
    }

    [Fact]
    public async Task Подпись_за_роль_не_свою_отклоняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedSignableAsync(db);
        var сервис = Сервис(db);

        // Инициатор обладает правом ManageProcurementProtocol, но подписать за
        // утверждающего (курирующего члена Правления) он не вправе.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.SignAsync(
                стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.Approver}, стенд.Initiator));
    }

    [Fact]
    public async Task Роль_без_закреплённого_подписанта_подписать_нельзя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        // Без инициирующего подразделения куратор организатора не определён.
        var стенд = await SeedSignableAsync(db, сКуратором: false);
        var сервис = Сервис(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => сервис.SignAsync(
                стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.OrganizerCurator}, стенд.Initiator));
    }

    [Fact]
    public async Task Три_закреплённые_подписи_утверждают_протокол()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedSignableAsync(db);
        var сервис = Сервис(db);

        await сервис.SignAsync(стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.Initiator}, стенд.Initiator);
        await сервис.SignAsync(стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.OrganizerCurator}, стенд.OrganizerCurator);
        var итог = await сервис.SignAsync(стенд.RequestId, new ProtocolSignRequest {Role = ProtocolSignerRole.Approver}, стенд.Approver);

        Assert.Equal(ProtocolStatus.Approved, итог.Status);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int RequestId, int Initiator, int OrganizerCurator, int Approver, int Stranger);

    private static IProtocolService Сервис(DelosferaDbContext db)
    {
        var audit = new AuditService(db);
        var clock = new delosfera_server.Common.Services.BankClock();

        return new ProtocolService(
            db, new ProposalService(db, audit, clock), audit, clock,
            new NumeratorService(db));
    }

    private static User NewUser(string name) => new()
    {
        FullName = name,
        Email = $"proto-{Guid.NewGuid():N}@keremetbank.kg",
        PasswordHash = "x",
    };

    /// <summary>
    /// Готовый к подписанию протокол с закреплёнными сторонами: инициатор (автор
    /// карточки), куратор организатора (куратор инициирующего подразделения) и
    /// утверждающий (курирующий закупку член Правления). Виза УПиА заполнена, чтобы
    /// снять блокеры подписания; победитель — с наименьшей ценой, основание не нужно.
    /// </summary>
    private static async Task<Стенд> SeedSignableAsync(DelosferaDbContext db, bool сКуратором = true)
    {
        var инициатор = NewUser("Инициатор закупки");
        var кураторОрганизатора = NewUser("Куратор организатора");
        var утверждающий = NewUser("Зампред Правления");
        var посторонний = NewUser("Посторонний");
        db.Users.AddRange(инициатор, кураторОрганизатора, утверждающий, посторонний);
        await db.SaveChangesAsync();

        int? unitId = null;
        if (сКуратором)
        {
            var подразделение = new OrganizationUnit
            {
                TitleRu = "Инициирующее подразделение",
                CuratorUserId = кураторОрганизатора.Id,
            };
            db.Set<OrganizationUnit>().Add(подразделение);
            await db.SaveChangesAsync();
            unitId = подразделение.Id;
        }

        var победитель = new Supplier {Title = "ОсОО Победитель", Inn = "01234567890123"};
        var второй = new Supplier {Title = "ОсОО Второй", Inn = "01234567890124"};
        db.Suppliers.AddRange(победитель, второй);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = "OnApproval",
            AuthorId = инициатор.Id,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Серверное оборудование",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 5_000_000m,
            MethodId = method.Id,
            ProtocolRequired = true,
            InitiatorUnitId = unitId,
            CuratorUserId = утверждающий.Id,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var tender = new Tender {RequestId = request.Id, Status = TenderStatus.Decided};
        db.Tenders.Add(tender);
        await db.SaveChangesAsync();

        db.TenderBids.AddRange(
            new TenderBid
            {
                TenderId = tender.Id, SupplierId = победитель.Id,
                Price = 4_800_000m, IsAdmitted = true, IsWinner = true,
            },
            new TenderBid
            {
                TenderId = tender.Id, SupplierId = второй.Id,
                Price = 4_950_000m, IsAdmitted = true,
            });
        await db.SaveChangesAsync();

        var сервис = Сервис(db);
        await сервис.GenerateAsync(request.Id, инициатор.Id);
        // Виза УПиА о наличии средств — иначе протокол не подписывается (блокер).
        await сервис.UpdateAsync(
            request.Id,
            new ProtocolUpdateRequest {BudgetNote = "Средства предусмотрены бюджетом"},
            инициатор.Id);

        return new Стенд(request.Id, инициатор.Id, кураторОрганизатора.Id, утверждающий.Id, посторонний.Id);
    }
}
