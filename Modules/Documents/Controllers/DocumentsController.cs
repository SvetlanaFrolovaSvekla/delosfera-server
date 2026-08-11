using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Documents.DTO.Response;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>
/// Единый реестр документов (карточка + журнал аудита). Контурные модули
/// (СЗ/ВНД/закупки) создают документы через свои эндпоинты; здесь — общий доступ.
/// </summary>
[ApiController]
[Route("api/documents")]
[Tags("Документы")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;

    public DocumentsController(IDocumentService service) => _service = service;

    /// <summary>Карточка документа с вложениями.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> Get(int id)
    {
        var doc = await _service.GetAsync(id);
        if (doc is null) return NotFound();

        return Ok(new DocumentResponse
        {
            Id = doc.Id,
            Type = doc.Type.ToString(),
            RegNumber = doc.RegNumber,
            Title = doc.Title,
            StatusCode = doc.StatusCode,
            AuthorId = doc.AuthorId,
            CurrentRouteInstanceId = doc.CurrentRouteInstanceId,
            IsPaperCarrier = doc.IsPaperCarrier,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            Attachments = doc.Attachments.Select(a => new DocumentAttachmentResponse
            {
                Id = a.Id, FileName = a.FileName, Hash = a.Hash, Size = a.Size,
                IsPrimary = a.IsPrimary, CreatedAt = a.CreatedAt
            }).ToList()
        });
    }

    /// <summary>Журнал аудита документа (хронология значимых действий, GEN-13).</summary>
    [HttpGet("{id:int}/audit")]
    [ProducesResponseType(typeof(List<AuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditEntryResponse>>> Audit(int id)
    {
        var entries = await _service.GetAuditAsync(id);
        return Ok(entries.Select(e => new AuditEntryResponse
        {
            Id = e.Id, Action = e.Action, UserId = e.UserId, At = e.At, PayloadJson = e.PayloadJson
        }).ToList());
    }
}
