using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Signing.Services;

namespace delosfera_server.Modules.Signing.Controllers;

/// <summary>
/// Регламент применения простой электронной подписи и согласие с ним.
///
/// Права не проверяются: регламент читает и принимает каждый сотрудник, который
/// собирается что-то подписывать, — то есть любой участник согласования.
/// </summary>
[ApiController]
[Authorize]
[Route("api/signing/regulation")]
public class SimpleSignatureRegulationController : ControllerBase
{
    private readonly ISimpleSignatureRegulationService _regulation;
    private readonly ICurrentUserService _currentUser;

    public SimpleSignatureRegulationController(
        ISimpleSignatureRegulationService regulation, ICurrentUserService currentUser)
    {
        _regulation = regulation;
        _currentUser = currentUser;
    }

    /// <summary>Текст регламента и состояние согласия текущего сотрудника.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _regulation.GetStateAsync(_currentUser.UserId, ct));

    /// <summary>Принять регламент указанной редакции.</summary>
    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptRegulationRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _regulation.AcceptAsync(_currentUser.UserId, request.Version, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}

public class AcceptRegulationRequest
{
    /// <summary>Редакция, которую сотрудник прочитал и принимает.</summary>
    public required string Version { get; set; }
}
