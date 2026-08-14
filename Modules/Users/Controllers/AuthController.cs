using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>
/// Аутентификация пользователей.
/// Refresh-токен передаётся только через httpOnly-cookie (недоступен из JS, защита от XSS),
/// access-токен возвращается в теле и хранится клиентом в памяти.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Аутентификация")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    private readonly IAuthService _authService;
    private readonly ILanguageResolver _languageResolver;
    private readonly int _refreshTokenExpiryDays;

    public AuthController(IAuthService authService, ILanguageResolver languageResolver, IConfiguration configuration)
    {
        _authService = authService;
        _languageResolver = languageResolver;
        _refreshTokenExpiryDays = int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "30");
    }

    /// <summary>
    /// Вход в систему по email и паролю
    /// </summary>
    /// <response code="200">Вход выполнен успешно, access-токен в теле, refresh — в httpOnly-cookie</response>
    /// <response code="401">Неверный email/пароль или учётная запись деактивирована</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _authService.LoginAsync(request, language);
            SetRefreshCookie(result.RefreshToken);
            return Ok(result.Response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Доменный вход через службу каталогов AD/LDAP (INT-01)
    /// </summary>
    /// <response code="200">Вход выполнен успешно</response>
    /// <response code="401">Каталог не подтвердил пару логин/пароль либо сотрудник не заведён в системе</response>
    [HttpPost("login-domain")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginDomain([FromBody] DomainLoginRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _authService.LoginWithDirectoryAsync(request, language);
            SetRefreshCookie(result.RefreshToken);
            return Ok(result.Response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Обновление access-токена по refresh-токену из httpOnly-cookie
    /// </summary>
    /// <response code="200">Токены обновлены успешно (новый refresh — в cookie)</response>
    /// <response code="401">Refresh-токен отсутствует, недействителен, истёк или отозван</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "Refresh-токен отсутствует" });

        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _authService.RefreshAsync(refreshToken, language);
            SetRefreshCookie(result.RefreshToken);
            return Ok(result.Response);
        }
        catch (UnauthorizedAccessException ex)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Выход из системы - отзывает refresh-токен и удаляет cookie
    /// </summary>
    /// <response code="204">Выход выполнен успешно</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await _authService.LogoutAsync(refreshToken);

        DeleteRefreshCookie();
        return NoContent();
    }

    // Refresh-cookie: httpOnly (не читается из JS), Secure на https, SameSite=Strict,
    // Path=/auth — браузер шлёт её только на эндпоинты аутентификации.
    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        MaxAge = TimeSpan.FromDays(_refreshTokenExpiryDays)
    };

    private void SetRefreshCookie(string refreshToken) =>
        Response.Cookies.Append(RefreshCookieName, refreshToken, RefreshCookieOptions());

    private void DeleteRefreshCookie()
    {
        var options = RefreshCookieOptions();
        options.MaxAge = TimeSpan.Zero;
        Response.Cookies.Delete(RefreshCookieName, options);
    }
}
