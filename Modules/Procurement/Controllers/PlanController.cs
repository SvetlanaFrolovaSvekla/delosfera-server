using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>Годовой План закупок и отчёт об исполнении (PRC-22).</summary>
[ApiController]
[Route("api/procurement/plans")]
[Tags("Закупки — План закупок")]
[Authorize]
public class PlanController : ControllerBase
{
    private readonly IPlanService _plans;
    private readonly ICurrentUserService _currentUser;

    public PlanController(IPlanService plans, ICurrentUserService currentUser)
    {
        _plans = plans;
        _currentUser = currentUser;
    }

    /// <summary>Годы, на которые заведён план.</summary>
    [HttpGet("years")]
    public async Task<IActionResult> Years() => Ok(await _plans.YearsAsync());

    /// <summary>
    /// Позиции утверждённых планов для выбора в заявке: поиск по коду и предмету.
    /// Позиции подразделения запрашивающего идут первыми.
    /// </summary>
    [HttpGet("items/search")]
    public async Task<IActionResult> SearchItems(
        [FromQuery] string? q, [FromQuery] int? orgUnitId, [FromQuery] int limit = 20) =>
        Ok(await _plans.SearchItemsAsync(q, orgUnitId, limit));

    /// <summary>План на год с фактом исполнения; 204, если не заведён.</summary>
    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year)
    {
        var plan = await _plans.GetAsync(year);
        return plan is null ? NoContent() : Ok(plan);
    }

    /// <summary>Завести план на год.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageProcurementPlan)]
    public async Task<IActionResult> Create([FromBody] PlanCreateRequest request) =>
        await Run(() => _plans.CreateAsync(request, _currentUser.UserId));

    /// <summary>Добавить позицию плана.</summary>
    [HttpPost("{planId:int}/items")]
    [RequirePermission(PermissionCode.ManageProcurementPlan)]
    public async Task<IActionResult> AddItem(int planId, [FromBody] PlanItemRequest request) =>
        await Run(() => _plans.AddItemAsync(planId, request, _currentUser.UserId));

    /// <summary>Исключить позицию, на которую ещё не ссылались заявки.</summary>
    [HttpDelete("items/{itemId:int}")]
    [RequirePermission(PermissionCode.ManageProcurementPlan)]
    public async Task<IActionResult> RemoveItem(int itemId) =>
        await Run(() => _plans.RemoveItemAsync(itemId, _currentUser.UserId));

    /// <summary>Утвердить план протоколом Правления.</summary>
    [HttpPost("{planId:int}/approve")]
    [RequirePermission(PermissionCode.ManageProcurementPlan)]
    public async Task<IActionResult> Approve(int planId, [FromBody] PlanApproveRequest request) =>
        await Run(() => _plans.ApproveAsync(planId, request, _currentUser.UserId));

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
