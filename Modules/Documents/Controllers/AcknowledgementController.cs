using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Documents.Controllers;

public class RefuseRequest
{
    public required string Reason { get; set; }
}

public class CancelEntryRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Ознакомление с документами (Б-19).
///
/// Два разных взгляда на одни данные: сотруднику нужен список того, что он обязан
/// прочитать, кадровой службе — кто прочитал, кто отказался и кто молчит. Поэтому
/// «мои ознакомления» и «лист целиком» — разные вызовы, а не один с фильтром.
/// </summary>
[ApiController]
[Authorize]
[Route("api/acknowledgements")]
[Tags("Ознакомление")]
public class AcknowledgementController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IAcknowledgementService _service;
    private readonly ICurrentUserService _currentUser;

    public AcknowledgementController(
        DelosferaDbContext db, IAcknowledgementService service, ICurrentUserService currentUser)
    {
        _db = db;
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Что я обязан прочитать. Отвеченное показывается ниже, но не исчезает.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] bool includeAnswered = false, CancellationToken ct = default)
    {
        var q = _db.AcknowledgementEntries
            .AsNoTracking()
            .Where(e => e.UserId == _currentUser.UserId && e.State != AcknowledgementState.Cancelled);

        if (!includeAnswered) q = q.Where(e => e.State == AcknowledgementState.Pending);

        var items = await q
            .OrderBy(e => e.State)
            .ThenBy(e => e.Sheet!.DueDate ?? DateOnly.MaxValue)
            .Select(e => new
            {
                e.Id,
                sheetId = e.SheetId,
                documentId = e.Sheet!.DocumentId,
                documentTitle = e.Sheet.Document!.Title,
                documentNumber = e.Sheet.Document.RegNumber,
                documentType = e.Sheet.Document.Type.ToString(),

                // Ссылка на карточку: у каждого типа свой раздел и свой
                // идентификатор — documentId в адресе не годится.
                entityId = e.Sheet.Document.Type == DocumentType.Sz
                    ? _db.SzDocuments.Where(z => z.DocumentId == e.Sheet.DocumentId)
                        .Select(z => (int?) z.Id).FirstOrDefault()
                    : e.Sheet.Document.Type == DocumentType.Procurement
                        ? _db.ProcurementRequests.Where(r => r.DocumentId == e.Sheet.DocumentId)
                            .Select(r => (int?) r.Id).FirstOrDefault()
                        : null,
                instruction = e.Sheet.Instruction,
                dueDate = e.Sheet.DueDate,
                requireSignature = e.Sheet.RequireSignature,
                state = e.State.ToString(),
                e.RespondedAt,
                e.Comment,
                e.CreatedAt,

                // Просрочка считается на сервере: у клиента может быть другой часовой
                // пояс, а срок ознакомления — вопрос дисциплины, а не отображения.
                overdue = e.State == AcknowledgementState.Pending
                          && e.Sheet.DueDate != null
                          && e.Sheet.DueDate < DateOnly.FromDateTime(DateTime.UtcNow),
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>Листы по документу — их может быть несколько: досылали разным составам.</summary>
    [HttpGet("document/{documentId:int}")]
    public async Task<IActionResult> ByDocument(int documentId, CancellationToken ct) =>
        Ok(await _db.AcknowledgementSheets
            .AsNoTracking()
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.Instruction,
                s.DueDate,
                s.RequireSignature,
                s.CreatedAt,
                s.ClosedAt,
                authorName = _db.Users.Where(u => u.Id == s.CreatedByUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                всего = s.Entries.Count(e => e.State != AcknowledgementState.Cancelled),
                ознакомились = s.Entries.Count(e => e.State == AcknowledgementState.Acknowledged),
                отказались = s.Entries.Count(e => e.State == AcknowledgementState.Refused),
                ждём = s.Entries.Count(e => e.State == AcknowledgementState.Pending),
            })
            .ToListAsync(ct));

    /// <summary>Лист целиком: кто прочитал, кто отказался, кто молчит.</summary>
    [HttpGet("{sheetId:int}")]
    public async Task<IActionResult> Sheet(int sheetId, CancellationToken ct)
    {
        var sheet = await _db.AcknowledgementSheets
            .AsNoTracking()
            .Where(s => s.Id == sheetId)
            .Select(s => new
            {
                s.Id,
                s.DocumentId,
                documentTitle = s.Document!.Title,
                documentNumber = s.Document.RegNumber,
                s.Instruction,
                s.DueDate,
                s.RequireSignature,
                s.CreatedAt,
                s.ClosedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (sheet is null) return NotFound(new {message = "Лист ознакомления не найден"});

        var entries = await _db.AcknowledgementEntries
            .AsNoTracking()
            .Where(e => e.SheetId == sheetId)
            .OrderBy(e => e.User!.FullName)
            .Select(e => new
            {
                e.Id,
                e.UserId,
                userName = e.User!.FullName,
                orgUnit = e.OrgUnit != null ? e.OrgUnit.TitleRu : null,
                state = e.State.ToString(),
                e.RespondedAt,
                e.Comment,
                e.SignatureId,
            })
            .ToListAsync(ct);

        return Ok(new {sheet, entries});
    }

    /// <summary>Завести лист ознакомления по документу.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSheetRequest request, CancellationToken ct) =>
        await Run(async () =>
        {
            var sheet = await _service.CreateAsync(request, _currentUser.UserId, ct);
            return new {sheet.Id, sheet.DocumentId};
        });

    /// <summary>Дослать лист тем, кого забыли или кто пришёл позже.</summary>
    [HttpPost("{sheetId:int}/participants")]
    public async Task<IActionResult> AddParticipants(
        int sheetId, [FromBody] AcknowledgementTargets targets, CancellationToken ct) =>
        await Run(async () => new
        {
            добавлено = await _service.AddParticipantsAsync(sheetId, targets, _currentUser.UserId, ct),
        });

    /// <summary>Ознакомлен — ставится подпись, если лист её требует.</summary>
    [HttpPost("entries/{entryId:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int entryId, CancellationToken ct) =>
        await Run(async () =>
        {
            await _service.AcknowledgeAsync(entryId, _currentUser.UserId, ct);
            return new {ok = true};
        });

    /// <summary>Отказ от ознакомления — с причиной.</summary>
    [HttpPost("entries/{entryId:int}/refuse")]
    public async Task<IActionResult> Refuse(
        int entryId, [FromBody] RefuseRequest request, CancellationToken ct) =>
        await Run(async () =>
        {
            await _service.RefuseAsync(entryId, _currentUser.UserId, request.Reason, ct);
            return new {ok = true};
        });

    /// <summary>Снять сотрудника с ознакомления.</summary>
    [HttpPost("entries/{entryId:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int entryId, [FromBody] CancelEntryRequest request, CancellationToken ct) =>
        await Run(async () =>
        {
            await _service.CancelAsync(entryId, _currentUser.UserId, request.Reason, ct);
            return new {ok = true};
        });

    [HttpPost("{sheetId:int}/close")]
    public async Task<IActionResult> Close(int sheetId, CancellationToken ct) =>
        await Run(async () =>
        {
            await _service.CloseAsync(sheetId, _currentUser.UserId, ct);
            return new {ok = true};
        });

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
