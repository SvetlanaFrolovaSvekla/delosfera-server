using delosfera_server.Modules.Sz.Models;

namespace Delosfera.Tests;

/// <summary>
/// Записки, заведённые до перестройки порядка.
///
/// Раньше регистрация шла первой: записка получала номер, и только потом её
/// согласовывали. Теперь наоборот. На стенде такие записки уже есть — с номером
/// и в согласовании, — и завершение их маршрута не должно отправлять их
/// «ждать регистрации»: регистрация выдала бы второй номер тому, что уже занесено
/// в книгу под первым.
///
/// Проверяем само правило перехода, без базы: развилка держится на двух
/// признаках — идёт ли подписание и есть ли номер.
/// </summary>
public class SzLegacyOrderTests
{
    /// <summary>
    /// Куда ведёт согласованная записка. Повторяет условие из
    /// SzRouteCompletionHandler: подписание, наличие номера, наличие адресата.
    /// </summary>
    private static string ПослеСогласования(bool подписание, bool ужеЗарегистрирована, bool естьАдресат) =>
        (подписание, ужеЗарегистрирована, естьАдресат) switch
        {
            (false, false, _) => SzStatus.PendingRegistration,
            (_, _, true) => SzStatus.OnAddresseeDecision,
            _ => SzStatus.OnExecution,
        };

    [Fact]
    public void Новая_записка_после_согласования_идёт_на_регистрацию()
    {
        var статус = ПослеСогласования(подписание: false, ужеЗарегистрирована: false, естьАдресат: true);

        Assert.Equal(SzStatus.PendingRegistration, статус);
    }

    [Fact]
    public void Записка_прежнего_порядка_не_идёт_на_повторную_регистрацию()
    {
        // Номер у неё уже есть: второй раз регистрировать нечего.
        var статус = ПослеСогласования(подписание: false, ужеЗарегистрирована: true, естьАдресат: true);

        Assert.NotEqual(SzStatus.PendingRegistration, статус);
        Assert.Equal(SzStatus.OnAddresseeDecision, статус);
    }

    [Fact]
    public void Записка_прежнего_порядка_без_адресата_идёт_на_исполнение()
    {
        var статус = ПослеСогласования(подписание: false, ужеЗарегистрирована: true, естьАдресат: false);

        Assert.Equal(SzStatus.OnExecution, статус);
    }

    [Fact]
    public void После_подписания_записка_идёт_к_адресату()
    {
        var статус = ПослеСогласования(подписание: true, ужеЗарегистрирована: true, естьАдресат: true);

        Assert.Equal(SzStatus.OnAddresseeDecision, статус);
    }

    [Fact]
    public void После_подписания_без_адресата_записка_идёт_на_исполнение()
    {
        var статус = ПослеСогласования(подписание: true, ужеЗарегистрирована: true, естьАдресат: false);

        Assert.Equal(SzStatus.OnExecution, статус);
    }

    [Fact]
    public void Прежний_статус_остаётся_известным_системе()
    {
        // На стенде под ним лежат записки: убрав его из перечня, мы сделали бы их
        // невидимыми в реестре и непроходимыми по фильтрам.
        Assert.Contains(SzStatus.Registered, SzStatus.All);
        Assert.Contains(SzStatus.Registered, SzStatus.Active);
    }
}
