using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>Настройки секретности ВНД — только для обладателей права ManageVndSecrecy
/// (администратор, главный редактор), которые и так видят любые секретные документы.</summary>
[ApiController]
[Route("api/vnd/{vndId:int}/secrecy")]
[Tags("ВНД — Секретность")]
[Authorize]
public class VndSecrecyController : ControllerBase
{
    private readonly IVndSecrecyService _service;
    private readonly ICurrentUserService _currentUser;

    public VndSecrecyController(IVndSecrecyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Текущие настройки секретности документа</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageVndSecrecy)]
    [ProducesResponseType(typeof(VndSecrecyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndSecrecyResponse>> Get(int vndId)
    {
        try { return Ok(await _service.GetAsync(vndId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Сохранить настройки секретности: признак, группы и отдельные пользователи</summary>
    [HttpPut]
    [RequirePermission(PermissionCode.ManageVndSecrecy)]
    [ProducesResponseType(typeof(VndSecrecyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndSecrecyResponse>> Update(int vndId, [FromBody] UpdateVndSecrecyRequest request)
    {
        try { return Ok(await _service.UpdateAsync(vndId, request, _currentUser.UserId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
