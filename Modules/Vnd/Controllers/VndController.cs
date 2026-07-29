using delosfera_server.Common.Services;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Services;
using Microsoft.AspNetCore.Authorization;

namespace delosfera_server.Modules.Vnd.Controllers;

[ApiController]
[Route("/vnd")]
[Tags("ВНД")]
[Authorize]
public class VndController : ControllerBase
{
    private readonly IVndService _service;
    private readonly ILanguageResolver _languageResolver;
    private readonly ICurrentUserService _currentUser;

    public VndController(IVndService service, ILanguageResolver languageResolver, ICurrentUserService currentUser)
    {
        _service = service;
        _languageResolver = languageResolver;
        _currentUser = currentUser;
    }

    /// <summary>Добавление новой ВНД</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VndResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VndResponse>> Create([FromBody] CreateVndRequest request)
    {
        try
        {
            var language = _languageResolver.Resolve(Request);
            var result = await _service.CreateAsync(request, _currentUser.UserId, language);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Расширенный поиск ВНД по всем фильтрам</summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<VndResponse>>> Search([FromBody] VndSearchRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.SearchAsync(request, language);
        return Ok(result);
    }

    /// <summary>Получить один ВНД по id</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VndResponse>> GetById(int id)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            return Ok(await _service.GetByIdAsync(id, language));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Добавление новой редакции ВНД</summary>
    [HttpPost("{vndId:int}/redactions")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<VndRedactionResponse>> AddRedaction(
        int vndId, [FromForm] CreateVndRedactionRequest request)
    {
        try
        {
            var result = await _service.AddRedactionAsync(vndId, request, _currentUser.UserId);
            return CreatedAtAction(nameof(GetById), new { id = vndId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}