using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Когда закупка обходится без договора — раздел VII Положения.
///
/// Порог по сумме здесь не главное правило, а последнее: Положение называет три
/// случая, когда договор нужен независимо от суммы. Проверять сумму первой
/// значило бы пропустить закупку у нерезидента на тысячу сом.
/// </summary>
public class ContractRequirementTests
{
    [Fact]
    public void Товары_до_пятидесяти_тысяч_идут_без_договора()
    {
        var решение = ContractRequirement.Decide(ProcurementSubjectKind.Goods, 40_000m);

        Assert.False(решение.Required);
        Assert.Contains("для товаров", решение.Reason);
    }

    [Fact]
    public void Товары_сверх_порога_требуют_договора()
    {
        var решение = ContractRequirement.Decide(ProcurementSubjectKind.Goods, 50_001m);

        Assert.True(решение.Required);
        Assert.Contains("превышает", решение.Reason);
    }

    [Fact]
    public void Ровно_порог_договора_не_требует()
    {
        // «Не превышающую пятьдесят тысяч» — граница входит в освобождение.
        Assert.False(ContractRequirement.Decide(ProcurementSubjectKind.Goods, 50_000m).Required);
        Assert.False(ContractRequirement.Decide(ProcurementSubjectKind.Services, 20_000m).Required);
    }

    [Fact]
    public void У_работ_и_услуг_порог_вдвое_с_половиной_ниже()
    {
        // Сорок тысяч: для товара это освобождение, для услуги — уже договор.
        Assert.False(ContractRequirement.Decide(ProcurementSubjectKind.Goods, 40_000m).Required);
        Assert.True(ContractRequirement.Decide(ProcurementSubjectKind.Works, 40_000m).Required);
        Assert.True(ContractRequirement.Decide(ProcurementSubjectKind.Services, 40_000m).Required);
    }

    [Theory]
    [InlineData(ProcurementSubjectKind.HouseholdGoods)]
    [InlineData(ProcurementSubjectKind.SpecificGoods)]
    [InlineData(ProcurementSubjectKind.GoodsWithInstallation)]
    public void Разновидности_товара_идут_по_товарному_порогу(ProcurementSubjectKind kind)
    {
        // Отдельный порог Положение называет только для работ и услуг; хозяйственный
        // и специфичный товар остаются товаром.
        Assert.False(ContractRequirement.Decide(kind, 45_000m).Required);
    }

    [Fact]
    public void Нерезидент_требует_договора_при_любой_сумме()
    {
        var решение = ContractRequirement.Decide(
            ProcurementSubjectKind.Goods, 1_000m, supplierIsNonResident: true);

        Assert.True(решение.Required);
        Assert.Contains("нерезидент", решение.Reason);
    }

    [Fact]
    public void Дробление_закупки_требует_договора()
    {
        var решение = ContractRequirement.Decide(
            ProcurementSubjectKind.Goods, 30_000m, hasRecentSimilar: true);

        Assert.True(решение.Required);
        Assert.Contains("консолидируется", решение.Reason);
    }

    [Fact]
    public void Руководитель_организатора_может_потребовать_договор()
    {
        var решение = ContractRequirement.Decide(
            ProcurementSubjectKind.Goods, 10_000m, demandedByProcurementHead: true);

        Assert.True(решение.Required);
        Assert.Contains("затребован", решение.Reason);
    }

    [Fact]
    public void Нерезидентство_проверяется_раньше_суммы()
    {
        // Оба основания сразу: в ответе должно быть названо то, что весомее, —
        // иначе сотрудник прочтёт «превышает порог» и решит, что дело в сумме.
        var решение = ContractRequirement.Decide(
            ProcurementSubjectKind.Goods, 900_000m, supplierIsNonResident: true);

        Assert.Contains("нерезидент", решение.Reason);
    }

    [Fact]
    public void Пороги_настраиваются_банком()
    {
        var решение = ContractRequirement.Decide(
            ProcurementSubjectKind.Goods, 40_000m, goodsThreshold: 30_000m);

        Assert.True(решение.Required);

        // Разделитель разрядов зависит от культуры сервера, а проверяется здесь
        // то, что назван новый предел, а не то, как он напечатан.
        var безРазделителей = System.Text.RegularExpressions.Regex.Replace(
            решение.Reason, @"[\s,  ]", "");

        Assert.Contains("30000", безРазделителей);
    }
}
