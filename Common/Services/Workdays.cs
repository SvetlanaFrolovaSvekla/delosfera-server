namespace delosfera_server.Common.Services;

/// <summary>
/// Счёт в рабочих днях.
///
/// Положение задаёт сроки закупки именно в них: конкурсный период не менее пяти
/// и не более тридцати, изучение заявок — не более десяти, рассмотрение
/// претензии — тоже десять. В календарных днях те же сроки получаются разными в
/// зависимости от того, на какой день недели пришлось начало.
///
/// Считаются только выходные. Праздники Кыргызской Республики переносятся
/// постановлением каждый год и в коде их держать нельзя: неверный список хуже
/// его отсутствия — он сдвигает срок молча и в обе стороны.
/// </summary>
public static class Workdays
{
    /// <summary>Прибавить рабочие дни: выходные в срок не входят.</summary>
    public static DateOnly Add(DateOnly from, int workdays)
    {
        var date = from;
        var added = 0;

        while (added < workdays)
        {
            date = date.AddDays(1);
            if (!Выходной(date)) added++;
        }

        return date;
    }

    /// <summary>
    /// Сколько рабочих дней между датами. Отрицательное — вторая дата раньше
    /// первой: так видно не только факт просрочки, но и её величину.
    /// </summary>
    public static int Between(DateOnly from, DateOnly to)
    {
        if (from == to) return 0;

        var (начало, конец, знак) = to > from ? (from, to, 1) : (to, from, -1);
        var count = 0;

        for (var d = начало.AddDays(1); d <= конец; d = d.AddDays(1))
            if (!Выходной(d)) count++;

        return count * знак;
    }

    private static bool Выходной(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
