using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Integrations.Controllers;

/// <summary>Служба каталогов AD/LDAP: синхронизация пользователей и оргструктуры (INT-01).</summary>
[ApiController]
[Route("api/integrations/directory")]
[Tags("Интеграции — служба каталогов")]
[Authorize]
public class DirectoryController : ControllerBase
{
    private readonly IDirectorySyncService _sync;
    private readonly ILdapDirectory _directory;
    private readonly ICurrentUserService _currentUser;

    public DirectoryController(
        IDirectorySyncService sync,
        ILdapDirectory directory,
        ICurrentUserService currentUser)
    {
        _sync = sync;
        _directory = directory;
        _currentUser = currentUser;
    }

    /// <summary>Состояние интеграции — включена ли она в конфигурации.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new {enabled = _directory.Enabled});

    /// <summary>
    /// Выгрузка сотрудников из каталога без записи в базу — проверка настроек
    /// перед первой синхронизацией.
    /// </summary>
    [HttpGet("preview")]
    [RequirePermission(PermissionCode.ManageUsers)]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        try
        {
            var entries = await _directory.ListUsersAsync(ct);
            return Ok(entries);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
        catch (Exception ex)
        {
            return StatusCode(502, new {message = $"Служба каталогов недоступна: {ex.Message}"});
        }
    }

    /// <summary>Запустить синхронизацию пользователей и оргструктуры.</summary>
    [HttpPost("sync")]
    [RequirePermission(PermissionCode.ManageUsers)]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        try
        {
            return Ok(await _sync.SyncAsync(_currentUser.UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
        catch (Exception ex)
        {
            return StatusCode(502, new {message = $"Служба каталогов недоступна: {ex.Message}"});
        }
    }
}
