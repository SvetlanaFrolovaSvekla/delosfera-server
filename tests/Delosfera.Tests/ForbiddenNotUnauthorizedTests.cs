using System.Security.Claims;
using delosfera_server.Common.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Отказ в праве — 403, отсутствие входа — 401.
///
/// Оба случая приходили из одного исключения и отдавали 401. Клиент по 401 идёт
/// обновлять токен и, не сумев, выходит из системы: попытка отозвать чужую
/// заявку или открыть закрытое письмо выглядела как конец сессии.
/// </summary>
public class ForbiddenNotUnauthorizedTests
{
    [Fact]
    public async Task Вошедшему_отказ_в_праве_даёт_403()
    {
        var context = Контекст(вошёл: true);

        await Обработчик(new UnauthorizedAccessException("Отозвать заявку может только её автор"))
            .InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Невошедшему_то_же_исключение_даёт_401()
    {
        var context = Контекст(вошёл: false);

        await Обработчик(new UnauthorizedAccessException("Пользователь не аутентифицирован!"))
            .InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Остальные_коды_не_изменились()
    {
        foreach (var (исключение, код) in new (Exception, int)[]
                 {
                     (new KeyNotFoundException("нет такой"), StatusCodes.Status404NotFound),
                     (new InvalidOperationException("нельзя"), StatusCodes.Status400BadRequest),
                     (new ArgumentException("не то"), StatusCodes.Status400BadRequest),
                 })
        {
            var context = Контекст(вошёл: true);

            await Обработчик(исключение).InvokeAsync(context);

            Assert.Equal(код, context.Response.StatusCode);
        }
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static ExceptionHandlingMiddleware Обработчик(Exception ex) =>
        new(_ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance);

    private static DefaultHttpContext Контекст(bool вошёл)
    {
        var context = new DefaultHttpContext {Response = {Body = new MemoryStream()}};

        context.User = вошёл
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "сотрудник")], "jwt"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        return context;
    }
}
