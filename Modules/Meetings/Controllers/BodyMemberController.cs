using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Meetings.Controllers;

/// <summary>
/// Состав коллегиальных органов: кто входит в Правление, КПА, Кредитный комитет.
///
/// Отдельная настройка, а не права роли. Банк меняет состав решением — протоколом
/// или приказом, — и перенастройка доступа тут ни при чём. Пока состав задавался
/// правами, роли с полным набором прав делали членами Правления администраторов
/// и редакторов ВНД, а председателя система искала по праву «выносить вопрос на
/// орган», которое по работе есть и у администратора системы.
///
/// Читать состав может каждый: в списке «Кому» и в повестке он и так виден.
/// Менять — тот, кто настраивает систему.
/// </summary>
[ApiController]
[Authorize]
[Route("api/meetings/body-members")]
[Tags("Заседания — Состав органов")]
public class BodyMemberController : ControllerBase
{
    private readonly IBodyMemberService _members;
    private readonly ICurrentUserService _currentUser;

    public BodyMemberController(IBodyMemberService members, ICurrentUserService currentUser)
    {
        _members = members;
        _currentUser = currentUser;
    }

    /// <summary>Состав органа; без параметра — всех органов сразу.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MeetingBody? body, CancellationToken ct) =>
        Ok(await _members.ListAsync(body, ct));

    /// <summary>Ввести человека в состав органа.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Add([FromBody] BodyMemberRequest request, CancellationToken ct) =>
        await Run(() => _members.AddAsync(request, _currentUser.UserId, ct));

    /// <summary>Изменить роль в органе, срок или основание.</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update(
        int id, [FromBody] BodyMemberRequest request, CancellationToken ct) =>
        await Run(() => _members.UpdateAsync(id, request, _currentUser.UserId, ct));

    /// <summary>Вывести из состава.</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        try
        {
            await _members.RemoveAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }
}
