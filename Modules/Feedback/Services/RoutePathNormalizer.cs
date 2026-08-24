using System.Text;

namespace delosfera_server.Modules.Feedback.Services;

/// <summary>
/// Приводит адрес страницы к шаблону маршрута.
///
/// «/base-vnd/17» и «/base-vnd/204» — один и тот же экран, открытый на разных
/// документах. Если хранить их как есть, отчёт о посещаемости превратится в список
/// документов, и на вопрос «какими разделами пользуются» не ответит.
///
/// Разбор идёт на сервере, а не в браузере, по двум причинам: браузер присылает то,
/// что ему велели, — доверять его разметке нельзя; и правило нормализации меняется
/// вместе с маршрутами, а обновить сервер быстрее, чем дождаться, пока у всех
/// перезагрузится вкладка.
/// </summary>
public static class RoutePathNormalizer
{
    /// <summary>Длиннее этого пути в системе нет — всё прочее либо ошибка, либо попытка засорить таблицу.</summary>
    private const int MaxLength = 200;

    /// <summary>
    /// Возвращает шаблон маршрута и идентификатор объекта, если он был в адресе.
    /// Строка запроса отбрасывается целиком: в ней бывает то, что человек искал.
    /// </summary>
    public static (string RoutePath, int? EntityId) Normalize(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return ("/", null);

        var path = rawPath.Trim();

        // Отрезаем строку запроса и якорь до всякого разбора.
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        // Абсолютная ссылка — берём только путь. Заодно отсекает попытку прислать
        // ссылку на чужой сайт и увидеть её потом в отчёте как наш экран.
        //
        // Проверка на «://» здесь не для красоты: без неё Uri.TryCreate с
        // UriKind.Absolute на Unix принимает «/sz/17» за абсолютный файловый путь,
        // разбирает его и попутно перекодирует всё непечатное в проценты. На Windows
        // такого не происходит — и разбор одного и того же адреса зависел бы от того,
        // где запущено приложение.
        if (path.Contains("://", StringComparison.Ordinal)
            && Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            path = absolute.AbsolutePath;
        }

        if (!path.StartsWith('/'))
            path = "/" + path;

        if (path.Length > MaxLength)
            path = path[..MaxLength];

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return ("/", null);

        int? entityId = null;
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            builder.Append('/');

            if (int.TryParse(segment, out var number) && number > 0)
            {
                // Первое число в адресе и есть объект экрана. Второе — что-то
                // вложенное; для отчёта о посещаемости оно роли не играет.
                entityId ??= number;
                builder.Append(":id");
                continue;
            }

            builder.Append(Sanitize(segment));
        }

        return (builder.ToString(), entityId);
    }

    /// <summary>
    /// Оставляет в сегменте только то, из чего состоят наши маршруты. Значение
    /// попадёт в отчёт на экране администратора, и разметка оттуда нам не нужна.
    /// </summary>
    private static string Sanitize(string segment)
    {
        var builder = new StringBuilder(segment.Length);

        foreach (var ch in segment)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.')
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.Length == 0 ? "-" : builder.ToString();
    }
}
