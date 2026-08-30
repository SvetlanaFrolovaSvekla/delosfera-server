using delosfera_server.Common.Services;

namespace Delosfera.Tests;

/// <summary>
/// Счёт в рабочих днях.
///
/// Положение задаёт сроки закупки именно в них: конкурсный период не менее пяти
/// и не более тридцати, изучение заявок — не более десяти. В календарных днях те
/// же сроки выходят разными в зависимости от дня недели, на который пришлось
/// начало, и срок начинает зависеть от случайности.
/// </summary>
public class WorkdaysTests
{
    [Fact]
    public void Выходные_в_срок_не_входят()
    {
        // Пятница 4 сентября 2026 + 1 рабочий день = понедельник 7-е.
        var пятница = new DateOnly(2026, 9, 4);

        Assert.Equal(new DateOnly(2026, 9, 7), Workdays.Add(пятница, 1));
    }

    [Fact]
    public void Десять_рабочих_дней_это_две_недели()
    {
        // Со вторника 1 сентября срок изучения заявок истекает во вторник 15-го:
        // ровно две календарные недели, из которых четыре дня — выходные.
        var вторник = new DateOnly(2026, 9, 1);

        Assert.Equal(new DateOnly(2026, 9, 15), Workdays.Add(вторник, 10));
    }

    [Fact]
    public void Начало_в_субботу_отсчитывается_с_понедельника()
    {
        var суббота = new DateOnly(2026, 9, 5);

        Assert.Equal(new DateOnly(2026, 9, 7), Workdays.Add(суббота, 1));
    }

    [Fact]
    public void Между_датами_считаются_только_рабочие()
    {
        var понедельник = new DateOnly(2026, 9, 7);
        var следующий = new DateOnly(2026, 9, 14);

        // Неделя — это пять рабочих дней, а не семь.
        Assert.Equal(5, Workdays.Between(понедельник, следующий));
    }

    [Fact]
    public void Просрочка_считается_отрицательной()
    {
        var срок = new DateOnly(2026, 9, 7);
        var сегодня = new DateOnly(2026, 9, 14);

        // Так видно не только то, что срок прошёл, но и насколько.
        Assert.Equal(-5, Workdays.Between(сегодня, срок));
    }

    [Fact]
    public void Одна_и_та_же_дата_даёт_ноль()
    {
        var день = new DateOnly(2026, 9, 7);

        Assert.Equal(0, Workdays.Between(день, день));
    }
}
