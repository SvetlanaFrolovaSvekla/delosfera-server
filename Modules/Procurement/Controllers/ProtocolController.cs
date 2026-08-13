using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Протокол закупки (PRC-10): формирование из сравнительной таблицы, заполнение
/// разделов и подписание сторонами.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Протокол")]
[Authorize]
public class ProtocolController : ControllerBase
{
    private readonly IProtocolService _protocols;
    private readonly ICurrentUserService _currentUser;

    public ProtocolController(IProtocolService protocols, ICurrentUserService currentUser)
    {
        _protocols = protocols;
        _currentUser = currentUser;
    }

    /// <summary>Протокол по закупке; 204, если ещё не сформирован.</summary>
    [HttpGet("requests/{id:int}/protocol")]
    public async Task<IActionResult> Get(int id)
    {
        var protocol = await _protocols.GetAsync(id);
        return protocol is null ? NoContent() : Ok(protocol);
    }

    /// <summary>Сформировать или пересобрать протокол по текущей сравнительной таблице.</summary>
    [HttpPost("requests/{id:int}/protocol")]
    public async Task<IActionResult> Generate(int id) =>
        await Run(() => _protocols.GenerateAsync(id, _currentUser.UserId));

    /// <summary>Заполнить разделы: виза УПиА, оценка эксперта, особое мнение, основание выбора.</summary>
    [HttpPut("requests/{id:int}/protocol")]
    public async Task<IActionResult> Update(int id, [FromBody] ProtocolUpdateRequest request) =>
        await Run(() => _protocols.UpdateAsync(id, request, _currentUser.UserId));

    /// <summary>Подписать протокол от имени стороны.</summary>
    [HttpPost("requests/{id:int}/protocol/sign")]
    public async Task<IActionResult> Sign(int id, [FromBody] ProtocolSignRequest request) =>
        await Run(() => _protocols.SignAsync(id, request, _currentUser.UserId));

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
