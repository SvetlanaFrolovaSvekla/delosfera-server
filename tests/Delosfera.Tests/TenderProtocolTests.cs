using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Протокол по конкурсу.
///
/// Протокол собирался только из коммерческих предложений, а у конкурса их нет:
/// отбор там идёт по конкурсным заявкам. Оформить протокол по конкурсу было
/// нельзя вовсе — он отвечал «формируется после определения победителя», хотя
/// победитель был определён комиссией. Проверяем именно это: что источник строк
/// зависит от способа закупки и что голосование попадает в протокол.
/// </summary>
public class TenderProtocolTests
{
    private static Tender Конкурс(params TenderBid[] bids)
    {
        var tender = new Tender {Id = 1, RequestId = 1, Status = TenderStatus.Decided};
        foreach (var bid in bids)
        {
            bid.TenderId = tender.Id;
            tender.Bids.Add(bid);
        }
        return tender;
    }

    private static TenderBid Заявка(
        int id, string поставщик, decimal цена, bool допущена = true, bool победитель = false,
        string? причинаОтказа = null) =>
        new()
        {
            Id = id,
            SupplierId = id,
            Supplier = new Supplier {Id = id, Title = поставщик},
            Price = цена,
            IsAdmitted = допущена,
            IsWinner = победитель,
            RejectionReason = причинаОтказа,
        };

    private static CommissionVote Голос(int memberId, VoteChoice выбор) =>
        new() {MemberId = memberId, Choice = выбор};

    [Fact]
    public void Строки_протокола_берутся_из_конкурсных_заявок()
    {
        var tender = Конкурс(
            Заявка(1, "ОсОО Альфа", 850_000m),
            Заявка(2, "ОсОО Бета", 790_000m, победитель: true),
            Заявка(3, "ОсОО Гамма", 820_000m));

        var строки = TenderProtocolRows.Build(tender);

        Assert.Equal(3, строки.Count);
        Assert.Contains(строки, s => s.SupplierTitle == "ОсОО Бета" && s.IsWinner);
        Assert.All(строки, s => Assert.StartsWith("bid:", s.Key));
    }

    [Fact]
    public void Результаты_голосования_печатаются_в_заключении()
    {
        var заявка = Заявка(1, "ОсОО Бета", 790_000m, победитель: true);
        заявка.Votes.Add(Голос(1, VoteChoice.For));
        заявка.Votes.Add(Голос(2, VoteChoice.For));
        заявка.Votes.Add(Голос(3, VoteChoice.Against));
        заявка.Votes.Add(Голос(4, VoteChoice.Abstained));

        var строка = TenderProtocolRows.Build(Конкурс(заявка)).Single();

        Assert.Contains("за — 2", строка.Conclusion);
        Assert.Contains("против — 1", строка.Conclusion);
        Assert.Contains("воздержались — 1", строка.Conclusion);
    }

    [Fact]
    public void Голосования_не_было_видно_в_протоколе()
    {
        var строка = TenderProtocolRows.Build(Конкурс(Заявка(1, "ОсОО Альфа", 850_000m))).Single();

        Assert.Contains("голосование не проводилось", строка.Conclusion);
    }

    [Fact]
    public void Недопущенная_заявка_остаётся_в_таблице_с_основанием()
    {
        var строка = TenderProtocolRows
            .Build(Конкурс(Заявка(1, "ОсОО Альфа", 700_000m, допущена: false, причинаОтказа: "подана после срока")))
            .Single();

        // Опоздавшая заявка не исчезает из протокола: факт поступления фиксируется,
        // а основание недопуска — то, что потом объясняет решение комиссии.
        Assert.Contains("Не допущена", строка.Conclusion);
        Assert.Contains("подана после срока", строка.Conclusion);
        Assert.False(строка.Eligible);
    }

    [Fact]
    public void Недопущенная_заявка_не_становится_резервной()
    {
        var tender = Конкурс(
            Заявка(1, "ОсОО Дешёвый", 500_000m, допущена: false, причинаОтказа: "поставщик в чёрном списке"),
            Заявка(2, "ОсОО Бета", 790_000m, победитель: true),
            Заявка(3, "ОсОО Гамма", 820_000m));

        var строки = TenderProtocolRows.Build(tender);
        var победитель = строки.Single(s => s.IsWinner);

        var резерв = строки
            .Where(s => s.Eligible && s.Key != победитель.Key)
            .OrderBy(s => s.Price)
            .FirstOrDefault();

        // Резервным становится следующее по цене ДОПУЩЕННОЕ предложение: иначе при
        // отказе победителя договор ушёл бы тому, кого комиссия к отбору не допустила.
        Assert.NotNull(резерв);
        Assert.Equal("ОсОО Гамма", резерв!.SupplierTitle);
    }
}
