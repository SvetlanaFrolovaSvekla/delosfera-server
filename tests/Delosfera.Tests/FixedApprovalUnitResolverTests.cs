using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Подразделение фиксированного этапа ищется по номеру портала.
///
/// Номера были прописаны в коде, и это подвело: «Управление методологии»
/// оказалось дубликатом портального «Отдела методологии», при слиянии
/// справочника люди и документы переехали на портальную запись, а проверка
/// продолжала ждать прежний номер — согласующий из методологии не проходил
/// собственный этап.
/// </summary>
[Collection(PostgresCollection.Name)]
public class FixedApprovalUnitResolverTests
{
    private readonly PostgresFixture _postgres;

    public FixedApprovalUnitResolverTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Подразделение_находится_по_номеру_портала()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Номер в нашем справочнике произвольный — важен номер портала.
        var методология = await ПодразделениеАsync(db, "Отдел методологии", portalId: 65);

        var найдено = await new FixedApprovalUnitResolver(db)
            .ResolveAsync(ApprovalStageKind.Methodology);

        Assert.Equal(методология, найдено);
    }

    [Fact]
    public async Task Переезд_записи_внутри_справочника_ничего_не_ломает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var резолвер = new FixedApprovalUnitResolver(db);

        var было = await ПодразделениеАsync(db, "Отдел методологии", portalId: 65);
        Assert.Equal(было, await резолвер.ResolveAsync(ApprovalStageKind.Methodology));

        // Дубликат слили: запись удалена, а номер портала переехал на другую.
        await db.OrganizationUnits.Where(u => u.Id == было).ExecuteDeleteAsync();
        var стало = await ПодразделениеАsync(db, "Отдел методологии", portalId: 65);

        Assert.Equal(стало, await резолвер.ResolveAsync(ApprovalStageKind.Methodology));
        Assert.NotEqual(было, стало);
    }

    [Fact]
    public async Task Все_четыре_фиксированных_этапа_разрешаются()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ПодразделениеАsync(db, "Юридическое управление", portalId: 39);
        await ПодразделениеАsync(db, "Управление риск-менеджмента", portalId: 56);
        await ПодразделениеАsync(db, "Управление комплаенс контроля", portalId: 64);
        await ПодразделениеАsync(db, "Отдел методологии", portalId: 65);

        var резолвер = new FixedApprovalUnitResolver(db);

        foreach (var kind in new[]
                 {
                     ApprovalStageKind.Legal, ApprovalStageKind.RiskManagement,
                     ApprovalStageKind.Compliance, ApprovalStageKind.Methodology,
                 })
            Assert.NotNull(await резолвер.ResolveAsync(kind));
    }

    [Fact]
    public async Task Портальная_запись_важнее_прежнего_номера()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Синхронизация прошла и завела своё «Юридическое управление». Прежний
        // номер из сида никуда не делся, но отвечать за этап должна портальная
        // запись — на неё переезжают люди и документы.
        var портальное = await ПодразделениеАsync(db, "Юридическое управление", portalId: 39);

        var найдено = await new FixedApprovalUnitResolver(db).ResolveAsync(ApprovalStageKind.Legal);

        Assert.Equal(портальное, найдено);
        Assert.NotEqual(FixedApprovalOrgUnits.LegalOrgUnitId, найдено);
    }

    [Fact]
    public async Task Нефиксированный_этап_подразделения_не_имеет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        Assert.Null(await new FixedApprovalUnitResolver(db).ResolveAsync(ApprovalStageKind.Custom));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task<int> ПодразделениеАsync(
        DelosferaDbContext db, string title, int? portalId, int? id = null)
    {
        var unit = new OrganizationUnit {TitleRu = title, ExternalId = portalId};

        if (id is int fixedId) unit.Id = fixedId;

        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();

        return unit.Id;
    }
}
