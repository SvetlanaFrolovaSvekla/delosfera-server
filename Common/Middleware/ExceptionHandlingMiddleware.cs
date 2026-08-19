using System.Text.Json;

namespace delosfera_server.Common.Middleware;

/// <summary>
/// Единая точка обработки исключений. Доменные исключения переводятся в корректные
/// HTTP-коды с телом ProblemDetails-подобного вида, необработанные — в 500 без утечки
/// стектрейса клиенту (детали пишутся в лог). Позволяет контроллерам не дублировать
/// try/catch на каждый метод.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var status = ex switch
            {
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Необработанное исключение при обработке {Path}", context.Request.Path);
            else
                _logger.LogInformation("Обработанное доменное исключение ({Status}): {Message}", status, ex.Message);

            if (context.Response.HasStarted)
                throw;

            // 500 не раскрывает детали наружу; доменные ошибки отдают понятное сообщение.
            var message = status == StatusCodes.Status500InternalServerError
                ? "Внутренняя ошибка сервера"
                : ex.Message;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { status, message }));
        }
    }
}
