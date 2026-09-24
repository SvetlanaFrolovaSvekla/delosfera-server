using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Справочник: производственный календарь для сроков согласования редакций ВНД — рабочее время
/// банка и праздники. Читать может любой авторизованный
/// пользователь (клиенту нужны правила, чтобы показывать оставшееся рабочее время), менять —
/// только администратор справочников ВНД (главный редактор).
/// </summary>
[ApiController]
[Route("api/vnd/work-calendar")]
[Tags("ВНД — Производственный календарь")]
[Authorize]
public class VndWorkCalendarController : ControllerBase
{
    private readonly IVndWorkCalendarService _service;

    public VndWorkCalendarController(IVndWorkCalendarService service)
    {
        _service = service;
    }

    /// <summary>Записи календаря за год.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VndWorkCalendarDayResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<List<VndWorkCalendarDayResponse>>> GetYear([FromQuery] int year) =>
        Run(() => _service.GetYearAsync(year));

    /// <summary>Правила рабочего времени и исключения на период (для расчётов на клиенте).</summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(VndWorkCalendarRulesResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<VndWorkCalendarRulesResponse>> GetRules([FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        Run(() => _service.GetRulesAsync(from, to));

    [HttpPost]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(VndWorkCalendarDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<VndWorkCalendarDayResponse>> Create([FromBody] SaveVndWorkCalendarDayRequest request) =>
        Run(() => _service.CreateAsync(request));

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(VndWorkCalendarDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<VndWorkCalendarDayResponse>> Update(int id, [FromBody] SaveVndWorkCalendarDayRequest request) =>
        Run(() => _service.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Скопировать праздники одного года в другой (по тем же числам).</summary>
    [HttpPost("copy-year")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(CopyVndWorkCalendarYearResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<CopyVndWorkCalendarYearResponse>> CopyYear([FromBody] CopyVndWorkCalendarYearRequest request) =>
        Run(() => _service.CopyYearAsync(request));

    /// <summary>Рабочее время банка (минуты от полуночи по Бишкеку).</summary>
    [HttpGet("hours")]
    [ProducesResponseType(typeof(VndWorkHoursResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<VndWorkHoursResponse>> GetHours() => Run(() => _service.GetHoursAsync());

    /// <summary>Сменить рабочее время банка. Нормативы в днях сохраняют смысл, сроки текущих
    /// согласований пересчитываются.</summary>
    [HttpPut("hours")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(VndWorkHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<VndWorkHoursResponse>> UpdateHours([FromBody] UpdateVndWorkHoursRequest request) =>
        Run(() => _service.UpdateHoursAsync(request));

    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
