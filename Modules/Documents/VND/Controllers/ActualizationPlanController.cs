using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>Годовой план актуализации ВНД (PLN-01..07).</summary>
[ApiController]
[Route("api/actualization/plan")]
[Tags("Планирование актуализации")]
[Authorize]
public class ActualizationPlanController : ControllerBase
{
    private readonly IActualizationPlanService _plans;
    private readonly IActualizationPlanImportService _import;
    private readonly IPlanItemLifecycleService _lifecycle;
    private readonly ICurrentUserService _currentUser;

    public ActualizationPlanController(
        IActualizationPlanService plans,
        IActualizationPlanImportService import,
        IPlanItemLifecycleService lifecycle,
        ICurrentUserService currentUser)
    {
        _plans = plans;
        _import = import;
        _lifecycle = lifecycle;
        _currentUser = currentUser;
    }

    /// <summary>Годы, на которые заведён план.</summary>
    [HttpGet("years")]
    public async Task<IActionResult> Years() => Ok(await _plans.YearsAsync());

    /// <summary>План на год со светофором по срокам; 204, если не заведён.</summary>
    [HttpGet("{year:int}")]
    public async Task<IActionResult> Get(int year)
    {
        var plan = await _plans.GetAsync(year);
        return plan is null ? NoContent() : Ok(plan);
    }

    /// <summary>Завести план на год.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> Create([FromBody] PlanCreateRequest request) =>
        await Run(() => _plans.CreateAsync(request, _currentUser.UserId));

    /// <summary>Утвердить план.</summary>
    [HttpPost("{planId:int}/approve")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> Approve(int planId, [FromBody] PlanApproveRequest request) =>
        await Run(() => _plans.ApproveAsync(planId, request, _currentUser.UserId));

    /// <summary>Добавить позицию вручную.</summary>
    [HttpPost("{planId:int}/items")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> AddItem(int planId, [FromBody] PlanItemSaveRequest request) =>
        await Run(() => _plans.AddItemAsync(planId, request, _currentUser.UserId));

    /// <summary>Изменить реквизиты позиции.</summary>
    [HttpPut("items/{itemId:int}")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> UpdateItem(int itemId, [FromBody] PlanItemSaveRequest request) =>
        await Run(() => _plans.UpdateItemAsync(itemId, request, _currentUser.UserId));

    /// <summary>Перенести срок с указанием причины.</summary>
    [HttpPost("items/{itemId:int}/reschedule")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> Reschedule(int itemId, [FromBody] PlanItemRescheduleRequest request) =>
        await Run(() => _plans.RescheduleAsync(itemId, request, _currentUser.UserId));

    /// <summary>Снять позицию с плана.</summary>
    [HttpPost("items/{itemId:int}/exclude")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> Exclude(int itemId, [FromBody] PlanItemExcludeRequest request) =>
        await Run(() => _plans.ExcludeAsync(itemId, request, _currentUser.UserId));

    /// <summary>Журнал операций по позиции (PLN-07).</summary>
    [HttpGet("items/{itemId:int}/history")]
    public async Task<IActionResult> History(int itemId) => await Run(() => _plans.HistoryAsync(itemId));

    /// <summary>Запустить актуализацию по позиции — кнопка «Создать ТИД» (PLN-05).</summary>
    [HttpPost("items/{itemId:int}/start-actualization")]
    public async Task<IActionResult> StartActualization(
        int itemId, [FromBody] StartActualizationRequest request) =>
        await Run(() => _lifecycle.StartActualizationAsync(itemId, request, _currentUser.UserId));

    // ── импорт и отчётность ──────────────────────────────────────────────────

    /// <summary>Шаблон плана для заполнения в Excel.</summary>
    [HttpGet("template")]
    public IActionResult Template() =>
        File(_import.Template(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Шаблон плана актуализации.xlsx");

    /// <summary>Импорт плана из заполненного шаблона (PLN-01).</summary>
    [HttpPost("{year:int}/import")]
    [RequirePermission(PermissionCode.ViewVndActualizationPage)]
    public async Task<IActionResult> Import(int year, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new {message = "Файл не приложен"});

        await using var stream = file.OpenReadStream();

        try
        {
            return Ok(await _import.ImportAsync(year, stream, _currentUser.UserId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
        catch (Exception ex)
        {
            // Чужой файл может оказаться чем угодно — от .xls до переименованного архива.
            return BadRequest(new {message = $"Файл не разобран как книга Excel: {ex.Message}"});
        }
    }

    /// <summary>Отчёт по исполнительской дисциплине (PLN-07).</summary>
    [HttpGet("{year:int}/discipline-report")]
    public async Task<IActionResult> DisciplineReport(int year)
    {
        try
        {
            var bytes = await _import.DisciplineReportAsync(year);

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Исполнительская дисциплина {year}.xlsx");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
    }

    // ── настройки порогов ────────────────────────────────────────────────────

    /// <summary>Пороги индикации и сроки напоминаний (PLN-03, PLN-04).</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> Settings() => Ok(await _plans.GetSettingsAsync());

    /// <summary>Изменить пороги — их задаёт Отдел методологии.</summary>
    [HttpPut("settings")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    public async Task<IActionResult> SaveSettings([FromBody] ActualizationSettingsDto request) =>
        await Run(() => _plans.SaveSettingsAsync(request));

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
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new {message = ex.Message});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
