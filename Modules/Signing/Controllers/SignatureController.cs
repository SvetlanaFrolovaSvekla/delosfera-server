using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Signing.Services;

namespace delosfera_server.Modules.Signing.Controllers;

/// <summary>Подписание документов: КЭП на хеш версии вложения (SIG-01/02, INT-03).</summary>
[ApiController]
[Route("api/signing")]
[Tags("Подписание")]
[Authorize]
public class SignatureController : ControllerBase
{
    private readonly IQualifiedSignatureService _qualified;
    private readonly ICurrentUserService _currentUser;

    public SignatureController(IQualifiedSignatureService qualified, ICurrentUserService currentUser)
    {
        _qualified = qualified;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Данные для подписи: хеш версии файла и уже наложенные подписи.
    /// Подписывает его криптопровайдер на рабочем месте, закрытый ключ на сервер не передаётся.
    /// </summary>
    [HttpGet("attachments/{attachmentId:int}/challenge")]
    public async Task<IActionResult> Challenge(int attachmentId) =>
        await Run(() => _qualified.GetChallengeAsync(attachmentId));

    /// <summary>Принять квалифицированную подпись хеша версии.</summary>
    [HttpPost("attachments/{attachmentId:int}/qualified")]
    public async Task<IActionResult> Sign(int attachmentId, [FromBody] QualifiedSignRequest request) =>
        await Run(() => _qualified.SignAsync(attachmentId, request, _currentUser.UserId));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
