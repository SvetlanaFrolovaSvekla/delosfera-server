using delosfera_server.Common.Services;
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
/// Подразделения маршрута закупки ищутся по номеру портала.
///
/// Бюджетный контроль искался по названию «Управление стратегического
/// планирования и бюджетирования» — так называется наша пустая запись из сида, а
/// настоящее управление приходит из портала под именем «Управление планирования
/// и анализа». Этап уходил в подразделение без единого сотрудника, и заявка
/// вставала на согласовании, которое некому вынести.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProcurementRouteUnitLookupTests
{
    /// <summary>Номер бюджетного управления в портале банка.</summary>
    private const int BudgetPortalId = 59;

    private readonly PostgresFixture _postgres;

    public ProcurementRouteUnitLookupTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Бюджетный_этап_берёт_портальное_управление_по_номеру()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Синхронизация привела реальную оргструктуру: бюджетное управление
        // приходит из портала под номером 59 как «Управление планирования и
        // анализа». Маршрут находит его по номеру портала, а не по названию.
        var бюджетное = await db.OrganizationUnits
            .FirstAsync(u => u.ExternalId == BudgetPortalId);

        var начальник = await ПользовательАsync(db, "Кожомуратова Анара");
        бюджетное.HeadUserId = начальник;
        await db.SaveChangesAsync();

        Assert.Contains(начальник, await МаршрутАsync(db));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task<List<int?>> МаршрутАsync(DelosferaDbContext db)
    {
        var автор = await ПользовательАsync(db, "Инициатор заявки");
        var инициирующее = await ПодразделениеСРуководствомАsync(db, "Управление делами");

        await db.Users.Where(u => u.Id == автор)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.OrgUnitId, инициирующее));

        await ПодразделениеСРуководствомАsync(db, "Сектор закупок");
        await ПодразделениеСРуководствомАsync(db, "Административный отдел");

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = ProcurementStatus.Draft,
            AuthorId = автор,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = document.Id,
            Subject = "Мониторы",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 45_000m,
            MethodId = method.Id,
            InitiatorUnitId = инициирующее,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var audit = new AuditService(db);
        var engine = new RouteEngine(
            db, audit, [], new NoSubstitutions(), new SilentNotifier(), new FakeSignatures(), new RouteRoleResolver(db));

        var instance = await new ProcurementRouteService(db, engine).StartAsync(request, автор);

        return await db.RouteSteps
            .Where(s => s.RouteInstanceId == instance.Id)
            .SelectMany(s => s.Participants.Select(p => p.UserId))
            .ToListAsync();
    }

    /// <summary>Подразделение с назначенными начальником и куратором: без них маршрут не строится.</summary>
    private static async Task<int> ПодразделениеСРуководствомАsync(DelosferaDbContext db, string titleRu)
    {
        var unit = await db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == titleRu);

        if (unit is null)
        {
            unit = new OrganizationUnit {TitleRu = titleRu};
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();
        }

        unit.HeadUserId ??= await ПользовательАsync(db, $"Руководитель: {titleRu}");
        unit.CuratorUserId ??= await ПользовательАsync(db, $"Куратор: {titleRu}");
        await db.SaveChangesAsync();

        return unit.Id;
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
