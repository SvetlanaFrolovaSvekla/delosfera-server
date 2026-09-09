using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.PowerOfAttorney.DTO;
using delosfera_server.Modules.PowerOfAttorney.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.PowerOfAttorney.Controllers;

/// <summary>
/// Доверенности: кто, кому, на что и на какой срок.
///
/// Смотрят многие — вопрос «вправе ли он это подписать» возникает у юристов,
/// закупок и делопроизводства. Выдают и отзывают единицы.
/// </summary>
[ApiController]
[Authorize]
[Route("api/poa")]
[Tags("Доверенности")]
public class PoaController : ControllerBase
{
    private readonly IPoaService _poa;
    private readonly ICurrentUserService _currentUser;

    public PoaController(IPoaService poa, ICurrentUserService currentUser)
    {
        _poa = poa;
        _currentUser = currentUser;
    }

    /// <summary>Реестр доверенностей с фильтрами.</summary>
    [HttpPost("search")]
    [RequirePermission(PermissionCode.ViewPowersOfAttorney)]
    public async Task<IActionResult> Search([FromBody] PoaFilterRequest filter, CancellationToken ct) =>
        Ok(await _poa.SearchAsync(filter, ct));

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCode.ViewPowersOfAttorney)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        try { return Ok(await _poa.GetAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    /// <summary>
    /// Чем человек вправе распоряжаться на указанный день. Отдельная ручка, потому
    /// что этот вопрос задают перед подписанием, а не при разборе реестра.
    /// </summary>
    [HttpGet("valid")]
    [RequirePermission(PermissionCode.ViewPowersOfAttorney)]
    public async Task<IActionResult> Valid(
        [FromQuery] int userId, [FromQuery] DateOnly? on, CancellationToken ct) =>
        Ok(await _poa.ValidForUserAsync(userId, on ?? DateOnly.FromDateTime(DateTime.UtcNow), ct));

    /// <summary>Мои действующие доверенности — видны без права на реестр.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(await _poa.ValidForUserAsync(_currentUser.UserId, DateOnly.FromDateTime(DateTime.UtcNow), ct));

    /// <summary>Что истекает в ближайшие дни: продлевают заранее, а не задним числом.</summary>
    [HttpGet("expiring")]
    [RequirePermission(PermissionCode.ViewPowersOfAttorney)]
    public async Task<IActionResult> Expiring([FromQuery] int days = 30, CancellationToken ct = default) =>
        Ok(await _poa.ExpiringAsync(days, ct));

    [HttpPost]
    [RequirePermission(PermissionCode.ManagePowersOfAttorney)]
    public async Task<IActionResult> Create([FromBody] PoaSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _poa.CreateAsync(request, _currentUser.UserId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManagePowersOfAttorney)]
    public async Task<IActionResult> Update(int id, [FromBody] PoaSaveRequest request, CancellationToken ct)
    {
        try { return Ok(await _poa.UpdateAsync(id, request, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }

    /// <summary>Выдать: присвоить номер по книге и перевести в действующие.</summary>
    /// <summary>
    /// Приложить скан доверенности. Реестр без скана отвечает на вопрос
    /// «вправе ли он подписать» одними реквизитами.
    /// </summary>
    [HttpPost("{id:int}/files")]
    [RequirePermission(PermissionCode.ManagePowersOfAttorney)]
    public async Task<IActionResult> AddFile(int id, IFormFile file, CancellationToken ct)
    {
        try { return Ok(await _poa.AddFileAsync(id, file, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    /// <summary>Сканы доверенности.</summary>
    [HttpGet("{id:int}/files")]
    [RequirePermission(PermissionCode.ViewPowersOfAttorney)]
    public async Task<IActionResult> Files(int id, CancellationToken ct) =>
        Ok(await _poa.FilesAsync(id, ct));

    [HttpPost("{id:int}/issue")]
    [RequirePermission(PermissionCode.ManagePowersOfAttorney)]
    public async Task<IActionResult> Issue(int id, CancellationToken ct)
    {
        try { return Ok(await _poa.IssueAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }

    /// <summary>Отозвать с обоснованием. Передоверия по ней отзываются вместе с ней.</summary>
    [HttpPost("{id:int}/revoke")]
    [RequirePermission(PermissionCode.ManagePowersOfAttorney)]
    public async Task<IActionResult> Revoke(int id, [FromBody] PoaRevokeRequest request, CancellationToken ct)
    {
        try { return Ok(await _poa.RevokeAsync(id, request.Reason, request.On, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }
}
