using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Integrations.Controllers;

/// <summary>
/// Почтовые уведомления: слать письма или нет и куда.
///
/// Свой адрес, а не приставка к настройкам службы каталогов: разделы соседние
/// в интерфейсе, но никак не связаны, и общий путь однажды заставил бы искать
/// почту в каталоге.
/// </summary>
[ApiController]
[Route("api/system/mail")]
[Authorize]
public class MailSettingsController(IMailSettingsService mail) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<MailSettingsDto>> Get(CancellationToken ct) =>
        Ok(await mail.GetAsync(ct));

    [HttpPut]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<MailSettingsDto>> Save(
        [FromBody] MailSettingsRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mail.SaveAsync(request, ct));
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new {message = e.Message});
        }
    }
}
