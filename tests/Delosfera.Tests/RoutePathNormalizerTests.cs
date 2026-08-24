using delosfera_server.Modules.Feedback.Services;

namespace Delosfera.Tests;

/// <summary>
/// Приведение адреса страницы к шаблону маршрута.
///
/// Проверяем не форму, а последствия: сгруппируются ли заходы на один экран, не
/// утечёт ли в отчёт строка запроса и не сломается ли разбор на том, что пришлёт
/// браузер, которому мы не хозяева.
/// </summary>
public class RoutePathNormalizerTests
{
    [Theory]
    [InlineData("/base-vnd/17", "/base-vnd/:id", 17)]
    [InlineData("/sz/431", "/sz/:id", 431)]
    [InlineData("/users/8", "/users/:id", 8)]
    public void Число_в_адресе_становится_шаблоном(string raw, string expectedPath, int expectedId)
    {
        var (path, id) = RoutePathNormalizer.Normalize(raw);

        Assert.Equal(expectedPath, path);
        Assert.Equal(expectedId, id);
    }

    [Fact]
    public void Разные_документы_дают_один_экран()
    {
        // Ради этого всё и затевалось: иначе отчёт о посещаемости превратился бы
        // в список документов и на вопрос «какими разделами пользуются» не ответил.
        var first = RoutePathNormalizer.Normalize("/base-vnd/17").RoutePath;
        var second = RoutePathNormalizer.Normalize("/base-vnd/9204").RoutePath;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Строка_запроса_отбрасывается()
    {
        // В ней бывает то, что человек искал, — в банке это может быть фамилия
        // клиента или номер счёта. Ради статистики посещений такой риск не берут.
        var (path, _) = RoutePathNormalizer.Normalize("/search?q=Иванов%20И.И.&page=2");

        Assert.Equal("/search", path);
        Assert.DoesNotContain("Иванов", path);
    }

    [Fact]
    public void Якорь_отбрасывается()
    {
        var (path, _) = RoutePathNormalizer.Normalize("/help#article-5");

        Assert.Equal("/help", path);
    }

    [Fact]
    public void Абсолютный_адрес_сводится_к_пути()
    {
        var (path, id) = RoutePathNormalizer.Normalize("https://edo-test.keremetbank.kg/sz/12");

        Assert.Equal("/sz/:id", path);
        Assert.Equal(12, id);
    }

    [Fact]
    public void Чужой_сайт_не_попадает_в_отчёт_как_наш_экран()
    {
        // Присланное браузером — не доверенные данные. От абсолютной ссылки должен
        // остаться только путь, без имени хоста.
        var (path, _) = RoutePathNormalizer.Normalize("https://example.com/evil");

        Assert.Equal("/evil", path);
        Assert.DoesNotContain("example.com", path);
    }

    [Fact]
    public void Разбор_не_зависит_от_платформы()
    {
        // Uri.TryCreate с UriKind.Absolute на Unix принимает «/sz/17» за абсолютный
        // файловый путь и перекодирует непечатное в проценты, а на Windows — нет.
        // Поэтому абсолютной ссылка считается только при наличии схемы.
        var (path, id) = RoutePathNormalizer.Normalize("/sz/17");

        Assert.Equal("/sz/:id", path);
        Assert.Equal(17, id);
        Assert.DoesNotContain("%", path);
    }

    [Fact]
    public void Разметка_в_сегменте_вычищается()
    {
        // Значение попадёт в отчёт на экране администратора.
        // Закрывающий тег содержит косую черту и потому делится на два сегмента —
        // это нормально: путь всё равно ни с одним настоящим маршрутом не совпадёт.
        var (path, _) = RoutePathNormalizer.Normalize("/sz/<script>alert(1)</script>");

        Assert.Equal("/sz/scriptalert1/script", path);
        Assert.DoesNotContain("<", path);
        Assert.DoesNotContain(">", path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void Пустой_адрес_даёт_корень(string? raw)
    {
        var (path, id) = RoutePathNormalizer.Normalize(raw);

        Assert.Equal("/", path);
        Assert.Null(id);
    }

    [Fact]
    public void Относительный_адрес_получает_ведущую_косую()
    {
        var (path, _) = RoutePathNormalizer.Normalize("prc/plan");

        Assert.Equal("/prc/plan", path);
    }

    [Fact]
    public void Регистр_приводится_к_нижнему()
    {
        // Иначе /Base-VND и /base-vnd считались бы разными экранами.
        var (path, _) = RoutePathNormalizer.Normalize("/Base-VND");

        Assert.Equal("/base-vnd", path);
    }

    [Fact]
    public void Первое_число_считается_объектом_экрана()
    {
        var (path, id) = RoutePathNormalizer.Normalize("/meetings/44/agenda/7");

        Assert.Equal("/meetings/:id/agenda/:id", path);
        Assert.Equal(44, id);
    }

    [Fact]
    public void Ноль_и_отрицательные_не_считаются_объектом()
    {
        var (_, id) = RoutePathNormalizer.Normalize("/sz/0");

        Assert.Null(id);
    }

    [Fact]
    public void Длинный_адрес_обрезается_до_размера_колонки()
    {
        // Колонка в базе — 200 символов. Строка длиннее уронила бы сохранение
        // пачки целиком, а вместе с ней и переходы всех остальных экранов.
        var raw = "/" + new string('a', 500);

        var (path, _) = RoutePathNormalizer.Normalize(raw);

        Assert.True(path.Length <= 200, $"длина {path.Length}");
    }
}
