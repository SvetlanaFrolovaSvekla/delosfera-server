using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>Поставщики, благонадёжность и чёрный список (PRC-07/17).</summary>
[ApiController]
[Route("api/procurement/suppliers")]
[Tags("Закупки — Поставщики")]
[Authorize]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _suppliers;
    private readonly ICurrentUserService _currentUser;

    public SupplierController(ISupplierService suppliers, ICurrentUserService currentUser)
    {
        _suppliers = suppliers;
        _currentUser = currentUser;
    }

    /// <summary>Реестр поставщиков; blacklistedOnly=true — только чёрный список.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? query, [FromQuery] bool? blacklistedOnly) =>
        await Run(() => _suppliers.ListAsync(query, blacklistedOnly));

    /// <summary>Завести или изменить поставщика.</summary>
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] SupplierUpsertRequest request) =>
        await Run(() => _suppliers.UpsertAsync(request, _currentUser.UserId));

    /// <summary>Включить в чёрный список недобросовестных поставщиков.</summary>
    [HttpPost("{id:int}/blacklist")]
    public async Task<IActionResult> Blacklist(int id, [FromBody] BlacklistRequest request) =>
        await Run(() => _suppliers.BlacklistAsync(id, request, _currentUser.UserId));

    /// <summary>Снять ограничение досрочно.</summary>
    [HttpDelete("{id:int}/blacklist")]
    public async Task<IActionResult> RemoveFromBlacklist(int id) =>
        await Run(() => _suppliers.RemoveFromBlacklistAsync(id, _currentUser.UserId));

    /// <summary>Зафиксировать заключение ДБ о благонадёжности.</summary>
    [HttpPost("{id:int}/reliability")]
    public async Task<IActionResult> Reliability(int id, [FromBody] ReliabilityRequest request) =>
        await Run(() => _suppliers.SetReliabilityAsync(id, request, _currentUser.UserId));

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
