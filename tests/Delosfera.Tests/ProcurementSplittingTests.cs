using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Признаки дробления закупки (п. 10.3 Положения).
///
/// Дробление — это когда одну потребность подают несколькими заявками, каждая
/// ниже порога: три раза по 45 000 вместо одного раза по 135 000, и протокол не
/// составляется ни разу. Отследить это глазами нельзя, заявки подают разные люди
/// в разные недели, поэтому похожие закупки подразделения ищет система.
/// </summary>
public class ProcurementSplittingTests
{
    [Fact]
    public void Слова_предмета_соединяются_через_ИЛИ()
    {
        var запрос = ProcurementRequestService.BuildSimilarityQuery("Картриджи для принтеров партия 1");

        // Именно ИЛИ: с «И» запрос требовал бы совпадения слова «партия 1»,
        // и вторая партия той же закупки не нашлась бы — то есть ровно тот
        // случай, ради которого поиск и заводился.
        Assert.NotNull(запрос);
        Assert.Contains(" | ", запрос);
        Assert.Contains("картриджи", запрос);
        Assert.Contains("принтеров", запрос);
    }

    [Fact]
    public void Короткие_слова_и_номера_партий_отбрасываются()
    {
        var запрос = ProcurementRequestService.BuildSimilarityQuery("Картриджи для принтеров партия 1");

        Assert.DoesNotContain("для", запрос);
        Assert.DoesNotContain("1", запрос);
    }

    [Fact]
    public void Номер_партии_не_мешает_совпадению()
    {
        var первая = ProcurementRequestService.BuildSimilarityQuery("Картриджи для принтеров партия 1");
        var вторая = ProcurementRequestService.BuildSimilarityQuery("Картриджи для принтеров партия 2");

        // Запросы у двух партий одной закупки совпадают — значит каждая найдёт
        // другую независимо от номера.
        Assert.Equal(первая, вторая);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("и в на")]
    [InlineData("1 2 3")]
    public void Пустой_предмет_не_даёт_запроса(string? предмет)
    {
        // Опереться не на что: искать по одному предлогу — вывалить организатору
        // весь реестр и приучить его не читать подсказку.
        Assert.Null(ProcurementRequestService.BuildSimilarityQuery(предмет));
    }

    [Fact]
    public void Слов_берётся_не_больше_восьми()
    {
        var длинный = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"слово{i}тест"));

        var запрос = ProcurementRequestService.BuildSimilarityQuery(длинный);

        Assert.NotNull(запрос);
        Assert.Equal(8, запрос!.Split(" | ").Length);
    }

    [Fact]
    public void Повторы_не_раздувают_запрос()
    {
        var запрос = ProcurementRequestService.BuildSimilarityQuery("Мебель мебель МЕБЕЛЬ офисная");

        Assert.Equal(["мебель", "офисная"], запрос!.Split(" | "));
    }
}
