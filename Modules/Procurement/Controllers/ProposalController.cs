using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Коммерческие предложения по закупке (PRC-09/11/12): регистрация, заключение
/// о технических требованиях, сравнительная таблица и определение победителя.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Коммерческие предложения")]
[Authorize]
public class ProposalController : ControllerBase
{
    private readonly IProposalService _proposals;
    private readonly ICurrentUserService _currentUser;

    public ProposalController(IProposalService proposals, ICurrentUserService currentUser)
    {
        _proposals = proposals;
        _currentUser = currentUser;
    }

    /// <summary>Сравнительная таблица предложений по заявке.</summary>
    [HttpGet("requests/{id:int}/proposals")]
    public async Task<IActionResult> Comparison(int id) =>
        await Run(() => _proposals.GetComparisonAsync(id));

    /// <summary>Зарегистрировать коммерческое предложение.</summary>
    [HttpPost("requests/{id:int}/proposals")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Add(int id, [FromBody] ProposalCreateRequest request) =>
        await Run(() => _proposals.AddAsync(id, request, _currentUser.UserId));

    /// <summary>Заключение о соответствии техническим требованиям.</summary>
    [HttpPost("proposals/{proposalId:int}/verdict")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Verdict(int proposalId, [FromBody] ProposalVerdictRequest request) =>
        await Run(() => _proposals.SetVerdictAsync(proposalId, request, _currentUser.UserId));

    /// <summary>Удалить ошибочно заведённое предложение.</summary>
    /// <summary>Файлы предложения и ссылка на облако банка.</summary>
    [HttpPut("proposals/{proposalId:int}/sources")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Sources(int proposalId, [FromBody] ProposalSourcesRequest request) =>
        await Run(() => _proposals.SetSourcesAsync(proposalId, request, _currentUser.UserId));

    [HttpDelete("proposals/{proposalId:int}")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Delete(int proposalId) =>
        await Run(() => _proposals.DeleteAsync(proposalId, _currentUser.UserId));

    /// <summary>Определить победителя закупки.</summary>
    [HttpPost("requests/{id:int}/proposals/{proposalId:int}/winner")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Winner(int id, int proposalId) =>
        await Run(() => _proposals.DeclareWinnerAsync(id, proposalId, _currentUser.UserId));

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
