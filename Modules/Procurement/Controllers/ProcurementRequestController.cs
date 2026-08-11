using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Реестр заявок на закупку (PRC-01/03): поиск, счётчики, создание мастером,
/// отправка на согласование.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Заявки")]
[Authorize]
public class ProcurementRequestController : ControllerBase
{
    private readonly IProcurementRequestService _requests;
    private readonly ICurrentUserService _currentUser;

    public ProcurementRequestController(IProcurementRequestService requests, ICurrentUserService currentUser)
    {
        _requests = requests;
        _currentUser = currentUser;
    }

    /// <summary>Реестр закупок с фильтрами и серверной пагинацией.</summary>
    [HttpPost("requests/search")]
    public async Task<IActionResult> Search([FromBody] ProcurementSearchRequest request) =>
        await Run(() => _requests.SearchAsync(request, _currentUser.UserId));

    /// <summary>Счётчики вкладок реестра.</summary>
    [HttpGet("requests/counters")]
    public async Task<IActionResult> Counters() =>
        await Run(() => _requests.CountersAsync(_currentUser.UserId));

    /// <summary>Карточка заявки.</summary>
    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> Get(int id) => await Run(() => _requests.GetAsync(id));

    /// <summary>Создать заявку: способ и состав согласования подставляет Матрица полномочий.</summary>
    [HttpPost("requests")]
    public async Task<IActionResult> Create([FromBody] ProcurementCreateRequest request) =>
        await Run(() => _requests.CreateAsync(request, _currentUser.UserId));

    /// <summary>Отправить заявку на согласование.</summary>
    [HttpPost("requests/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id) =>
        await Run(() => _requests.SubmitAsync(id, _currentUser.UserId));

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
