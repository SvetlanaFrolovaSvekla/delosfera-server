using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Справочник: нормативы сроков согласования редакции ВНД по умолчанию
/// ("Первичное согласование", "Согласование после внесённых изменений", "Финальная выдержка").
/// Читать может любой авторизованный пользователь — значения нужны модалке запуска
/// согласования; менять — только администратор справочников ВНД.
/// </summary>
[ApiController]
[Route("api/vnd/approval-norm-settings")]
[Tags("ВНД — Нормативы согласования")]
[Authorize]
public class VndApprovalNormSettingsController : ControllerBase
{
    private readonly IVndApprovalNormSettingsService _service;

    public VndApprovalNormSettingsController(IVndApprovalNormSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(VndApprovalNormSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndApprovalNormSettingsResponse>> Get()
        => Ok(await _service.GetAsync());

    /// <summary>Сохранить нормативы по умолчанию.</summary>
    /// <response code="200">Нормативы сохранены</response>
    /// <response code="409">Значение вне диапазона (0; 90 дней]</response>
    [HttpPut]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(VndApprovalNormSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VndApprovalNormSettingsResponse>> Update(
        [FromBody] UpdateVndApprovalNormSettingsRequest request)
    {
        try
        {
            return Ok(await _service.UpdateAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
