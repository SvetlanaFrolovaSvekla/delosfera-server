using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>Замещение на период отсутствия (GEN-14).</summary>
[ApiController]
[Route("api/substitutions")]
[Tags("Пользователи — Замещение")]
[Authorize]
public class SubstitutionController : ControllerBase
{
    private readonly ISubstitutionService _substitutions;
    private readonly ICurrentUserService _currentUser;

    public SubstitutionController(ISubstitutionService substitutions, ICurrentUserService currentUser)
    {
        _substitutions = substitutions;
        _currentUser = currentUser;
    }

    /// <summary>Список замещений; mine=true — только связанные с текущим пользователем.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool mine = false) =>
        await Run(() => _substitutions.ListAsync(mine ? _currentUser.UserId : null));

    /// <summary>Оформить замещение.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubstitutionCreateRequest request) =>
        await Run(() => _substitutions.CreateAsync(request, _currentUser.UserId));

    /// <summary>Отменить замещение досрочно.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) =>
        await Run(() => _substitutions.CancelAsync(id, _currentUser.UserId));

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
        catch (ArgumentException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
