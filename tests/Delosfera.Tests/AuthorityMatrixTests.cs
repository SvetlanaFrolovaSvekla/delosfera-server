using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Матрица полномочий закупок (PRC-04) — по Положению о закупках, протокол № 23(8).
///
/// Ошибка здесь означает закупку, проведённую не тем способом и утверждённую не тем
/// органом: суммы в разы больше стоимости всей системы. Проверяются границы порогов,
/// а не середины диапазонов: ломается всегда на границе.
///
/// Данные матрицы приходят из миграций, поэтому тесты идут на реальной базе.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AuthorityMatrixTests
{
    private readonly PostgresFixture _postgres;

    public AuthorityMatrixTests(PostgresFixture postgres) => _postgres = postgres;

    [Theory]
    // Малые суммы — упрощённая закупка, утверждает куратор.
    [InlineData(50_000)]
    [InlineData(499_999)]
    public async Task SmallAmount_IsSimpleProcurement(decimal amount)
    {
        var result = await ResolveAsync(amount);

        Assert.Equal(ProcurementMethodCode.Simple.ToString(), result.MethodCode);
        Assert.False(result.CommissionRequired);
    }

    [Fact]
    public async Task AtHalfMillion_SwitchesToTender()
    {
        // Граница из Положения: от 500 000 сом закупка идёт конкурсом.
        var below = await ResolveAsync(499_999);
        var atThreshold = await ResolveAsync(500_000);

        Assert.Equal(ProcurementMethodCode.Simple.ToString(), below.MethodCode);
        Assert.StartsWith("Tender", atThreshold.MethodCode);
        Assert.True(atThreshold.CommissionRequired);
    }

    [Fact]
    public async Task LargeAmount_RequiresBoardOrHigher()
    {
        // 25 млрд — больше 20% активов банка: утверждает не Правление, а высший орган.
        var huge = await ResolveAsync(25_000_000_000);

        Assert.True(
            huge.ApprovalAuthority is ApprovalAuthority.SupervisoryBoard or ApprovalAuthority.Shareholders,
            $"При сумме 25 млрд орган утверждения не может быть «{huge.ApprovalAuthorityTitle}»");
    }

    [Fact]
    public async Task AuthorityGrows_WithAmount()
    {
        // Орган утверждения не должен «понижаться» с ростом суммы — это тот дефект,
        // который проще всего внести правкой одного порога.
        decimal[] amounts = [100_000, 5_000_000, 500_000_000, 25_000_000_000];
        var authorities = new List<ApprovalAuthority>();

        foreach (var amount in amounts)
            authorities.Add((await ResolveAsync(amount)).ApprovalAuthority);

        for (var i = 1; i < authorities.Count; i++)
        {
            Assert.True(authorities[i] >= authorities[i - 1],
                $"Для {amounts[i]:N0} орган утверждения ниже, чем для {amounts[i - 1]:N0}");
        }
    }

    [Fact]
    public async Task DirectContract_IsNeverChosenAutomatically()
    {
        // Прямое заключение — исключение из Положения и выбирается только вручную
        // с обоснованием. Автоподбор такого способа сам по себе был бы нарушением.
        decimal[] amounts = [10_000, 100_000, 1_000_000, 100_000_000];

        foreach (var amount in amounts)
        {
            var result = await ResolveAsync(amount);
            Assert.NotEqual(ProcurementMethodCode.Direct.ToString(), result.MethodCode);
        }
    }

    [Fact]
    public async Task ProtocolRequired_AboveThreshold()
    {
        var result = await ResolveAsync(1_000_000);

        Assert.True(result.ProtocolRequired);
        Assert.True(result.ProtocolThreshold > 0);

        var small = await ResolveAsync(result.ProtocolThreshold - 1);
        Assert.False(small.ProtocolRequired);
    }

    [Fact]
    public async Task AffiliatedDeal_SwitchesScale()
    {
        // Сделка с аффилированным лицом считается по проценту чистого собственного
        // капитала, а не по абсолютной шкале: при той же сумме требования строже.
        const decimal amount = 100_000_000;

        var ordinary = await ResolveAsync(amount);
        var affiliated = await ResolveAsync(amount, isAffiliated: true);

        Assert.True(affiliated.ApprovalAuthority >= ordinary.ApprovalAuthority,
            "Аффилированная сделка не может требовать более низкого органа утверждения");
    }

    private async Task<MatrixResolveResponse> ResolveAsync(decimal amount, bool isAffiliated = false)
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var service = new AuthorityMatrixService(db);

        return await service.ResolveAsync(new MatrixResolveRequest
        {
            Amount = amount,
            IsAffiliated = isAffiliated,
        });
    }
}
