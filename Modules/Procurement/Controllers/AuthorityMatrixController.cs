using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Матрица полномочий по закупкам (PRC-02/04/05): подбор способа закупки по сумме
/// и приложение №1 Положения целиком.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Матрица полномочий")]
[Authorize]
public class AuthorityMatrixController : ControllerBase
{
    private readonly IAuthorityMatrixService _matrix;
    private readonly ICurrentUserService _currentUser;

    public AuthorityMatrixController(IAuthorityMatrixService matrix, ICurrentUserService currentUser)
    {
        _matrix = matrix;
        _currentUser = currentUser;
    }

    /// <summary>Матрица целиком с пересчётом процентных порогов в сомы.</summary>
    [HttpGet("matrix")]
    public async Task<IActionResult> Table() => Ok(await _matrix.GetTableAsync());

    /// <summary>Подобрать способ закупки, состав согласования и орган утверждения по сумме.</summary>
    [HttpPost("matrix/resolve")]
    public async Task<IActionResult> Resolve([FromBody] MatrixResolveRequest request)
    {
        try
        {
            return Ok(await _matrix.ResolveAsync(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── настройка ────────────────────────────────────────────────────────────
    //
    // Пороги сумм, минимум коммерческих предложений и состав согласования банк
    // меняет сам решением по Положению — раньше эти значения жили только в коде и
    // правились сборкой. Настройку ведёт тот, кому доверены справочники закупок.

    /// <summary>Правила матрицы в сыром виде — для экрана настройки.</summary>
    [HttpGet("matrix/rules")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public async Task<IActionResult> Rules() => Ok(await _matrix.RulesForEditAsync());

    /// <summary>Способы закупки для настройки: минимум КП и подписи.</summary>
    [HttpGet("methods")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public async Task<IActionResult> Methods() => Ok(await _matrix.MethodsForEditAsync());

    [HttpPost("matrix/rules")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public Task<IActionResult> CreateRule([FromBody] MatrixRuleSaveRequest request) =>
        Run(() => _matrix.CreateRuleAsync(request, _currentUser.UserId));

    [HttpPut("matrix/rules/{id:int}")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public Task<IActionResult> UpdateRule(int id, [FromBody] MatrixRuleSaveRequest request) =>
        Run(() => _matrix.UpdateRuleAsync(id, request, _currentUser.UserId));

    [HttpDelete("matrix/rules/{id:int}")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public async Task<IActionResult> DeleteRule(int id)
    {
        try
        {
            await _matrix.DeleteRuleAsync(id, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    [HttpPut("methods/{id:int}")]
    [RequirePermission(PermissionCode.ManageProcurementDictionaries)]
    public Task<IActionResult> UpdateMethod(int id, [FromBody] ProcurementMethodSaveRequest request) =>
        Run(() => _matrix.UpdateMethodAsync(id, request, _currentUser.UserId));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (ArgumentException ex) { return BadRequest(new {message = ex.Message}); }
    }
}
