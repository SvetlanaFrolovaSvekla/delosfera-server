using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>
/// Аутентификация пользователей
/// </summary>
[ApiController]
[Route("/auth")]
[Tags("Аутентификация")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILanguageResolver _languageResolver;

    public AuthController(IAuthService authService, ILanguageResolver languageResolver)
    {
        _authService = authService;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Вход в систему по email и паролю
    /// </summary>
    /// <response code="200">Вход выполнен успешно, возвращены токены</response>
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
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Обновление access-токена по refresh-токену
    /// </summary>
    /// <response code="200">Токены обновлены успешно</response>
    /// <response code="401">Refresh-токен недействителен, истёк или отозван</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _authService.RefreshAsync(request, language);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Выход из системы - отзывает refresh-токен
    /// </summary>
    /// <response code="204">Выход выполнен успешно</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }
}