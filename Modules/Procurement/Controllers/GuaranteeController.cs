using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Гарантийные обеспечения (PRC-20), претензионная работа (PRC-21)
/// и пакет публикации конкурса (INT-05).
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Обеспечения и претензии")]
[Authorize]
public class GuaranteeController : ControllerBase
{
    private readonly IGuaranteeService _guarantees;
    private readonly IClaimService _claims;
    private readonly IPublicationService _publication;
    private readonly ICurrentUserService _currentUser;

    public GuaranteeController(
        IGuaranteeService guarantees, IClaimService claims,
        IPublicationService publication, ICurrentUserService currentUser)
    {
        _guarantees = guarantees;
        _claims = claims;
        _publication = publication;
        _currentUser = currentUser;
    }

    /// <summary>Реестр обеспечений; activeOnly=true — не возвращённые и не удержанные.</summary>
    [HttpGet("guarantees")]
    public async Task<IActionResult> Guarantees(
        [FromQuery] int? tenderId, [FromQuery] int? contractId, [FromQuery] bool? activeOnly) =>
        await Run(() => _guarantees.ListAsync(tenderId, contractId, activeOnly));

    /// <summary>Принять обеспечение конкурсной заявки (ГОКЗ) или исполнения договора (ГОИД).</summary>
    [HttpPost("guarantees")]
    public async Task<IActionResult> CreateGuarantee([FromBody] GuaranteeCreateRequest request) =>
        await Run(() => _guarantees.CreateAsync(request, _currentUser.UserId));

    /// <summary>Вернуть обеспечение либо удержать его с указанием основания.</summary>
    [HttpPost("guarantees/{id:int}/return")]
    public async Task<IActionResult> ReturnGuarantee(int id, [FromBody] GuaranteeReturnRequest request) =>
        await Run(() => _guarantees.ReturnAsync(id, request, _currentUser.UserId));

    /// <summary>Реестр претензий; openOnly=true — незакрытые.</summary>
    [HttpGet("claims")]
    public async Task<IActionResult> Claims([FromQuery] int? contractId, [FromQuery] bool? openOnly) =>
        await Run(() => _claims.ListAsync(contractId, openOnly));

    /// <summary>Зафиксировать нарушение условий договора.</summary>
    [HttpPost("contracts/{contractId:int}/claims")]
    public async Task<IActionResult> CreateClaim(int contractId, [FromBody] ClaimCreateRequest request) =>
        await Run(() => _claims.CreateAsync(contractId, request, _currentUser.UserId));

    /// <summary>Направить претензионное письмо контрагенту.</summary>
    [HttpPost("claims/{id:int}/send")]
    public async Task<IActionResult> SendClaim(int id, [FromBody] ClaimSendRequest request) =>
        await Run(() => _claims.SendAsync(id, request, _currentUser.UserId));

    /// <summary>Зафиксировать ответ контрагента.</summary>
    [HttpPost("claims/{id:int}/answer")]
    public async Task<IActionResult> AnswerClaim(int id, [FromBody] ClaimAnswerRequest request) =>
        await Run(() => _claims.AnswerAsync(id, request, _currentUser.UserId));

    /// <summary>Закрыть претензию: удовлетворена, передана в суд или отозвана.</summary>
    [HttpPost("claims/{id:int}/close")]
    public async Task<IActionResult> CloseClaim(int id, [FromBody] ClaimCloseRequest request) =>
        await Run(() => _claims.CloseAsync(id, request, _currentUser.UserId));

    /// <summary>Пакет публикации объявления о конкурсе для сайта и procurement.kg.</summary>
    [HttpGet("tenders/{tenderId:int}/publication")]
    public async Task<IActionResult> Publication(int tenderId) =>
        await Run(() => _publication.BuildAsync(tenderId));

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
