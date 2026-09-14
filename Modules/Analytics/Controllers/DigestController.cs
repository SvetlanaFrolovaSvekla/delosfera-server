using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Analytics.Services;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>Персональный дайджест по всем контурам (УВ-14).</summary>
[ApiController]
[Route("api/digest")]
[Tags("Дайджест")]
[Authorize]
public class DigestController : ControllerBase
{
    private readonly IDigestService _digest;
    private readonly ICurrentUserService _currentUser;

    public DigestController(IDigestService digest, ICurrentUserService currentUser)
    {
        _digest = digest;
        _currentUser = currentUser;
    }

    /// <summary>Сводка «что требует моего внимания».</summary>
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _digest.GetAsync(_currentUser.UserId));
}
