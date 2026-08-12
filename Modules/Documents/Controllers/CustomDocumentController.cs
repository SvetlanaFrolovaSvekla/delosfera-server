using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>Документы настраиваемых типов (GEN-06).</summary>
[ApiController]
[Route("api/custom-documents")]
[Tags("Документы настраиваемых типов")]
[Authorize]
public class CustomDocumentController : ControllerBase
{
    private readonly ICustomDocumentService _documents;
    private readonly ICurrentUserService _currentUser;

    public CustomDocumentController(ICustomDocumentService documents, ICurrentUserService currentUser)
    {
        _documents = documents;
        _currentUser = currentUser;
    }

    /// <summary>Журнал документов одного типа.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int definitionId) =>
        await Run(() => _documents.ListAsync(definitionId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => await Run(() => _documents.GetAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomDocumentSaveRequest request) =>
        await Run(() => _documents.CreateAsync(request, _currentUser.UserId));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomDocumentSaveRequest request) =>
        await Run(() => _documents.UpdateAsync(id, request));

    /// <summary>Отправить на согласование по шаблону маршрута из настроек типа.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id) =>
        await Run(() => _documents.SubmitAsync(id, _currentUser.UserId));

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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
