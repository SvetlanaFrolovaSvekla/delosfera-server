namespace delosfera_server.Common.Middleware;

/// <summary>
/// Заголовки безопасности ответов (NFR-03).
///
/// Настраиваются здесь, а не на обратном прокси: прокси в банке ставит служба
/// эксплуатации, и его конфигурация живёт отдельной жизнью. Заголовки, от которых
/// зависит безопасность приложения, должны ехать вместе с приложением.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Содержимое не должно интерпретироваться браузером вопреки типу: загруженный
        // сотрудником файл не может стать исполняемым скриптом.
        headers["X-Content-Type-Options"] = "nosniff";

        // Систему не встраивают в чужие страницы: иначе возможна подмена интерфейса
        // поверх настоящего (clickjacking).
        headers["X-Frame-Options"] = "DENY";

        // Реферер не утекает на внешние адреса: в пути карточки виден её идентификатор.
        headers["Referrer-Policy"] = "no-referrer";

        // /scalar и /openapi — это dev-страница документации API, ей нужны свои
        // скрипты и стили. Остальному приложению (реальным API-ответам) такое
        // послабление не требуется, поэтому сужаем его только на эти пути.
        var isDocsPath = context.Request.Path.StartsWithSegments("/scalar")
            || context.Request.Path.StartsWithSegments("/openapi");

        headers["Content-Security-Policy"] = isDocsPath
            ? "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
              "style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self'"
            : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await _next(context);
    }
}