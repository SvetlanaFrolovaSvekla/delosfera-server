using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
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
    private readonly IDocumentAttachmentService _attachments;
    private readonly ICurrentUserService _currentUser;

    public DocumentsController(
        IDocumentService service,
        IDocumentAttachmentService attachments,
        ICurrentUserService currentUser)
    {
        _service = service;
        _attachments = attachments;
        _currentUser = currentUser;
    }

    /// <summary>Вложения карточки: имя, хеш версии и состояние подписей (GEN-05, SIG-01).</summary>
    [HttpGet("{id:int}/attachments")]
    public async Task<IActionResult> Attachments(int id) => Ok(await _attachments.ListAsync(id));

    /// <summary>Приложить файл к карточке.</summary>
    [HttpPost("{id:int}/attachments")]
    public async Task<IActionResult> AddAttachment(int id, IFormFile file, [FromQuery] bool isPrimary = false) =>
        await Run(() => _attachments.AddAsync(id, file, _currentUser.UserId, isPrimary));

    /// <summary>
    /// Заменить файл новой версией. Подписи под прежней версией аннулируются:
    /// они удостоверяли другой текст (SIG-01).
    /// </summary>
    [HttpPut("attachments/{attachmentId:int}")]
    public async Task<IActionResult> ReplaceAttachment(int attachmentId, IFormFile file) =>
        await Run(() => _attachments.ReplaceAsync(attachmentId, file, _currentUser.UserId));

    /// <summary>Удалить вложение; подписи под ним аннулируются, но остаются в истории.</summary>
    [HttpDelete("attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId) =>
        await Run(async () => { await _attachments.DeleteAsync(attachmentId, _currentUser.UserId); return true; });

    /// <summary>Скачать вложение. Хеш сверяется при выдаче — подменённый файл не отдаётся.</summary>
    [HttpGet("attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        try
        {
            var (stream, contentType, fileName) = await _attachments.DownloadAsync(attachmentId);
            return File(stream, contentType, fileName);
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
