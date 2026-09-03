using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Из чего складывается сравнительная таблица протокола.
///
/// У конкурса отбор идёт по конкурсным заявкам, коммерческих предложений там нет
/// вовсе. Пока строки строились из заявок, а сверка «протокол устарел» смотрела
/// только в предложения, протокол конкурса сравнивался с пустотой: он выходил
/// устаревшим сразу после формирования, и пересборка это не снимала.
///
/// Проверяется чистое преобразование, без базы: именно оно решает, что попадёт
/// в протокол и с чем его потом сверят.
/// </summary>
public class TenderProtocolFreshnessTests
{
    [Fact]
    public void Конкурс_даёт_строку_на_каждую_заявку()
    {
        var tender = Конкурс();

        var варианты = TenderProtocolRows.Build(tender);

        Assert.Equal(3, варианты.Count);
        Assert.Equal(new[] {4_800_000m, 4_950_000m, 5_100_000m}, варианты.Select(v => v.Price));
    }

    [Fact]
    public void Победитель_конкурса_виден_в_вариантах()
    {
        var tender = Конкурс();

        var победитель = TenderProtocolRows.Build(tender).SingleOrDefault(v => v.IsWinner);

        // Ровно это и не находилось, когда протокол строился по предложениям:
        // победитель конкурса был определён комиссией, а протокол его не видел.
        Assert.NotNull(победитель);
        Assert.Equal(4_800_000m, победитель.Price);
    }

    [Fact]
    public void Недопущенная_заявка_остаётся_в_таблице_но_не_проходит_отбор()
    {
        var tender = Конкурс();
        tender.Bids.Single(b => b.Price == 5_100_000m).IsAdmitted = false;

        var варианты = TenderProtocolRows.Build(tender);

        // Протокол показывает всех, кто подавался, — иначе по нему не видно,
        // из чего выбирали. Но в расчёт наименьшей цены недопущенный не идёт.
        Assert.Equal(3, варианты.Count);
        Assert.False(варианты.Single(v => v.Price == 5_100_000m).Eligible);
    }

    [Fact]
    public void Простая_закупка_даёт_строку_на_каждое_предложение()
    {
        var comparison = new ProposalComparisonDto
        {
            MethodTitle = "Простая закупка",
            Proposals =
            [
                new ProposalDto {Id = 1, SupplierId = 1, SupplierTitle = "Первый", Price = 120_000, IsWinner = true, MeetsRequirements = true},
                new ProposalDto {Id = 2, SupplierId = 2, SupplierTitle = "Второй", Price = 130_000, MeetsRequirements = true},
            ],
        };

        var варианты = TenderProtocolRows.Build(comparison);

        Assert.Equal(2, варианты.Count);
        Assert.Equal("Первый", варианты.Single(v => v.IsWinner).SupplierTitle);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static Tender Конкурс() => new()
    {
        Id = 1,
        RequestId = 1,
        Status = TenderStatus.Decided,
        Bids =
        [
            Заявка(1, "ОсОО Альфа", 4_800_000m, winner: true),
            Заявка(2, "ОсОО Бета", 4_950_000m),
            Заявка(3, "ОсОО Гамма", 5_100_000m),
        ],
    };

    private static TenderBid Заявка(int id, string supplier, decimal price, bool winner = false) => new()
    {
        Id = id,
        SupplierId = id,
        Supplier = new Supplier {Id = id, Title = supplier},
        Price = price,
        IsAdmitted = true,
        IsWinner = winner,
        Votes = [],
    };
}
