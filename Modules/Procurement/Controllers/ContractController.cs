using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>Договор по итогам закупки и контроль исполнения (PRC-18/19).</summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Договор")]
[Authorize]
public class ContractController : ControllerBase
{
    private readonly IContractService _contracts;
    private readonly ICurrentUserService _currentUser;

    public ContractController(IContractService contracts, ICurrentUserService currentUser)
    {
        _contracts = contracts;
        _currentUser = currentUser;
    }

    /// <summary>Реестр договоров; requestId — договоры по конкретной закупке.</summary>
    [HttpGet("contracts")]
    public async Task<IActionResult> List([FromQuery] int? requestId) =>
        await Run(() => _contracts.ListAsync(requestId));

    /// <summary>Карточка договора.</summary>
    [HttpGet("contracts/{id:int}")]
    public async Task<IActionResult> Get(int id) => await Run(() => _contracts.GetAsync(id));

    /// <summary>Заключить договор с победителем закупки.</summary>
    [HttpPost("requests/{requestId:int}/contract")]
    public async Task<IActionResult> Create(int requestId, [FromBody] ContractCreateRequest request) =>
        await Run(() => _contracts.CreateAsync(requestId, request, _currentUser.UserId));

    /// <summary>Реквизиты договора: подписание, сроки поставки и оплаты, ответственный.</summary>
    [HttpPut("contracts/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ContractUpdateRequest request) =>
        await Run(() => _contracts.UpdateAsync(id, request, _currentUser.UserId));

    /// <summary>Зарегистрировать акт приёма-передачи или выполненных работ.</summary>
    [HttpPost("contracts/{id:int}/acts")]
    public async Task<IActionResult> AddAct(int id, [FromBody] DeliveryActRequest request) =>
        await Run(() => _contracts.AddActAsync(id, request, _currentUser.UserId));

    /// <summary>Утвердить акт: начальником СП либо курирующим членом Правления.</summary>
    [HttpPost("acts/{actId:int}/approve")]
    public async Task<IActionResult> ApproveAct(int actId, [FromQuery] bool asCurator = false) =>
        await Run(() => _contracts.ApproveActAsync(actId, asCurator, _currentUser.UserId));

    /// <summary>Расторгнуть договор с указанием основания.</summary>
    [HttpPost("contracts/{id:int}/terminate")]
    public async Task<IActionResult> Terminate(int id, [FromBody] ContractTerminateRequest request) =>
        await Run(() => _contracts.TerminateAsync(id, request, _currentUser.UserId));

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
