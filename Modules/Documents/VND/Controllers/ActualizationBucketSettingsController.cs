using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Справочник: пороги индикации сроков актуализации ВНД (Normal/Approaching/Critical).
/// "Просрочено" не настраивается — это всегда дата актуализации в прошлом.
/// </summary>
[ApiController]
[Route("api/vnd/actualization-bucket-settings")]
[Tags("ВНД — Пороги актуализации")]
[Authorize]
public class ActualizationBucketSettingsController : ControllerBase
{
    private readonly IActualizationBucketSettingsService _service;

    public ActualizationBucketSettingsController(IActualizationBucketSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ActualizationBucketSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActualizationBucketSettingsResponse>> Get()
        => Ok(await _service.GetAsync());

    /// <summary>Сохранить пороги.</summary>
    /// <response code="200">Пороги сохранены</response>
    /// <response code="409">Нарушены ограничения (отрицательные значения или Critical ≥ Approaching)</response>
    [HttpPut]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(ActualizationBucketSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActualizationBucketSettingsResponse>> Update(
        [FromBody] UpdateActualizationBucketSettingsRequest request)
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
