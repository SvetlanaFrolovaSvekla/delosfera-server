using delosfera_server.Common.Services;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Services;
using Microsoft.AspNetCore.Authorization;

namespace delosfera_server.Modules.Vnd.Controllers;

// TODO: добавить фильтр к базе ВНД поле ИНИЦИАТОР

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

    /// <summary>Сводка по срокам актуализации: сколько документов в норме,
    /// с приближающимся сроком, критичных и просроченных. Для дашборда планирования актуализации.</summary>
    [HttpGet("actualization/summary")]
    [ProducesResponseType(typeof(VndActualizationSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationSummaryResponse>> GetActualizationSummary()
    {
        return Ok(await _service.GetActualizationSummaryAsync());
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
    
    /// <summary>Получить список редакций ВНД</summary>
    [HttpGet("{vndId:int}/redactions")]
    [ProducesResponseType(typeof(List<VndRedactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<VndRedactionResponse>>> GetRedactions(int vndId)
    {
        try
        {
            return Ok(await _service.GetRedactionsAsync(vndId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    /// <summary>Отправить черновик редакции (редакция со статусом "Требуется согласование"
    /// на согласование</summary>
    [HttpPost("{vndId:int}/redactions/{redactionId:int}/submit")]
    [ProducesResponseType(typeof(VndRedactionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndRedactionResponse>> SubmitRedaction(int vndId, int redactionId)
    {
        try
        {
            return Ok(await _service.SubmitRedactionForApprovalAsync(vndId, redactionId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    
    /// <summary>Обновление реквизитов ВНД (кнопка "Изменить реквизиты")</summary>
    [HttpPut("{id:int}/requisites")]
    [ProducesResponseType(typeof(VndResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VndResponse>> UpdateRequisites(int id, [FromBody] UpdateVndRequisitesRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            return Ok(await _service.UpdateRequisitesAsync(id, request, language));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}