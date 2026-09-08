using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Матрица полномочий редактируется через интерфейс.
///
/// Пороги сумм, минимум коммерческих предложений и состав согласования жили
/// только в коде и правились сборкой — сектор закупок просил менять их сам по
/// Положению. Теперь правила и способы редактируются, а не задаются миграцией.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MatrixEditTests
{
    private const int Actor = 1;

    private readonly PostgresFixture _postgres;

    public MatrixEditTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Порог_правила_меняется()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        var правило = (await сервис.RulesForEditAsync()).First(r => !r.IsAffiliated);

        var запрос = Запрос(правило);
        запрос.MaxValue = 750_000m;
        await сервис.UpdateRuleAsync(правило.Id, запрос, Actor);

        var после = (await сервис.RulesForEditAsync()).First(r => r.Id == правило.Id);
        Assert.Equal(750_000m, после.MaxValue);
    }

    [Fact]
    public async Task Минимум_КП_у_способа_снижается_до_одного()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        var простой = (await сервис.MethodsForEditAsync()).First(m => m.Code == "Simple");
        Assert.Equal(3, простой.MinProposals);

        // Положение о закупках разрешает принять единственное КП.
        await сервис.UpdateMethodAsync(простой.Id, new ProcurementMethodSaveRequest
        {
            TitleRu = простой.TitleRu,
            ShortTitleRu = "Запрос ЦП",
            MinProposals = 1,
        }, Actor);

        var после = (await сервис.MethodsForEditAsync()).First(m => m.Id == простой.Id);
        Assert.Equal(1, после.MinProposals);
        Assert.Equal("Запрос ЦП", после.ShortTitleRu);
    }

    [Fact]
    public async Task Правило_с_пустыми_границами_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        // Правило без «от» и без «до» подходит под любую сумму и перекрывает всё.
        await Assert.ThrowsAsync<ArgumentException>(
            () => сервис.CreateRuleAsync(new MatrixRuleSaveRequest
            {
                MethodId = 2,
                ApprovalChainRu = "Куратор",
                MinValue = null, MaxValue = null,
            }, Actor));
    }

    [Fact]
    public async Task Верхняя_граница_ниже_нижней_не_принимается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => сервис.CreateRuleAsync(new MatrixRuleSaveRequest
            {
                MethodId = 2,
                ApprovalChainRu = "Куратор",
                MinValue = 500_000m, MaxValue = 100_000m,
            }, Actor));
    }

    [Fact]
    public async Task Погашенное_правило_в_подборе_не_участвует()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        // Гасим все обычные правила и проверяем, что подбор больше их не берёт.
        foreach (var r in (await сервис.RulesForEditAsync()).Where(r => !r.IsAffiliated))
            await сервис.DeleteRuleAsync(r.Id, Actor);

        var осталось = (await сервис.RulesForEditAsync())
            .Where(r => !r.IsAffiliated && r.IsActive)
            .ToList();

        Assert.Empty(осталось);
    }

    [Fact]
    public async Task Новое_правило_добавляется_и_попадает_в_подбор()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var сервис = Сервис(db);

        var было = (await сервис.RulesForEditAsync()).Count;

        await сервис.CreateRuleAsync(new MatrixRuleSaveRequest
        {
            MethodId = 4,
            IsAffiliated = false,
            MinValue = 1m, MaxValue = 300_000m,
            ApprovalChainRu = "Куратор",
            ApprovalAuthority = ApprovalAuthority.Curator,
            CommissionNoteRu = "Комиссия не создаётся",
            SortOrder = 5,
        }, Actor);

        Assert.Equal(было + 1, (await сервис.RulesForEditAsync()).Count);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static IAuthorityMatrixService Сервис(DelosferaDbContext db) =>
        new AuthorityMatrixService(db, new AuditService(db));

    private static MatrixRuleSaveRequest Запрос(MatrixRuleEditDto r) => new()
    {
        MethodId = r.MethodId,
        IsAffiliated = r.IsAffiliated,
        MinValue = r.MinValue, MinBase = r.MinBase,
        MaxValue = r.MaxValue, MaxBase = r.MaxBase,
        ApprovalChainRu = r.ApprovalChainRu,
        ApprovalAuthority = r.ApprovalAuthority,
        CommissionRequired = r.CommissionRequired,
        CommissionSize = r.CommissionSize,
        CommissionMinBoardMembers = r.CommissionMinBoardMembers,
        CommissionNoteRu = r.CommissionNoteRu,
        SortOrder = r.SortOrder,
        IsActive = r.IsActive,
    };
}
