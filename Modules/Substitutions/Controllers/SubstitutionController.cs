using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Controllers;

/// <summary>
/// Заявки на замещение (КСЗ-В9). Инициатор оформляет заявку с комиссией приёма-передачи,
/// УЧР исполняет (приказ на время замещения).
/// </summary>
[ApiController]
[Authorize]
[Route("api/substitution-requests")]
[Tags("Заявки на замещение")]
public class SubstitutionController : ControllerBase
{
    private readonly ISubstitutionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubstitutionStatisticsService _statistics;

    public SubstitutionController(
        ISubstitutionService service, ICurrentUserService currentUser,
        ISubstitutionStatisticsService statistics)
    {
        _service = service;
        _currentUser = currentUser;
        _statistics = statistics;
    }

    /// <summary>Реестр заявок; mineOnly — только свои. УЧР видит все.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query, [FromQuery] string? status, [FromQuery] bool mineOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        // Кто не ведёт кадровые СЗ, видит только свои заявки.
        if (!_currentUser.HasPermission(PermissionCode.ViewAllSz)) mineOnly = true;
        return Ok(await _service.SearchAsync(query, status, mineOnly, _currentUser.UserId, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var d = await _service.GetAsync(id, ct);
        return d is null ? NotFound(new { message = "Заявка на замещение не найдена" }) : Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubstitutionSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.CreateAsync(request, _currentUser.UserId, ct)); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SubstitutionSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.UpdateAsync(id, request, _currentUser.UserId, _currentUser.HasPermission(PermissionCode.ViewAllSz), ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отправить заявку — присваивается номер, уходит в УЧР на исполнение.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        try { return Ok(await _service.SubmitAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public record DecisionRequest(string? Comment);

    /// <summary>Согласовать текущий этап (директор филиала / Опер. управление / УЧР).</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] DecisionRequest? req, CancellationToken ct)
    {
        try { return Ok(await _service.ApproveAsync(id, _currentUser.UserId, req?.Comment, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отклонить заявку на текущем этапе — возвращается инициатору.</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] DecisionRequest? req, CancellationToken ct)
    {
        try { return Ok(await _service.RejectAsync(id, _currentUser.UserId, req?.Comment, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Исполнить заявку (УЧР): приказ оформлен.</summary>
    [HttpPost("{id:int}/execute")]
    [RequirePermission(PermissionCode.ViewAllSz)]
    public async Task<IActionResult> Execute(int id, CancellationToken ct)
    {
        try { return Ok(await _service.ExecuteAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        try { return Ok(await _service.WithdrawAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Печатная форма: form = order (приказ) или liability (договор МО).</summary>
    [HttpGet("{id:int}/print/{form}")]
    public async Task<IActionResult> Print(int id, string form, CancellationToken ct)
    {
        try
        {
            var (bytes, name) = await _service.PrintAsync(id, form, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, _currentUser.UserId, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // ── ЗМ-SLA: норматив срока согласования, статистика, выгрузка ──────────────

    /// <summary>Норматив срока согласования (рабочих дней на этап).</summary>
    [HttpGet("sla")]
    public async Task<IActionResult> GetSla(CancellationToken ct) =>
        Ok(new { approvalStepSlaDays = await _service.GetSlaDaysAsync(ct) });

    /// <summary>Задать норматив срока согласования. Ведёт администратор.</summary>
    [HttpPut("sla")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> SetSla([FromBody] SubstitutionSlaRequest request, CancellationToken ct)
    {
        try { return Ok(new { approvalStepSlaDays = await _service.SetSlaDaysAsync(request.ApprovalStepSlaDays, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Статистика по заявкам на замещение (в работе / просрочено / исполнено).</summary>
    [HttpGet("statistics")]
    [RequirePermission(PermissionCode.ViewAllSz)]
    public async Task<IActionResult> Statistics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _statistics.GetAsync(new SubstitutionStatisticsFilter { From = from, To = to }, ct));

    /// <summary>Выгрузка статистики в Excel.</summary>
    [HttpGet("statistics/export")]
    [RequirePermission(PermissionCode.ViewAllSz)]
    public async Task<IActionResult> StatisticsExport(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var bytes = await _statistics.ExportAsync(new SubstitutionStatisticsFilter { From = from, To = to }, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "substitutions-statistics.xlsx");
    }
}
