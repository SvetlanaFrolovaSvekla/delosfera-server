using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Authorization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Служебные записки (контур СЗ, срез 1): черновики, реестр, карточка,
/// отправка и регистрация делопроизводством.
/// </summary>
[ApiController]
[Route("api/sz")]
[Tags("СЗ — Служебные записки")]
[Authorize]
public class SzController : ControllerBase
{
    private readonly ISzService _sz;
    private readonly ISzExecutionService _execution;
    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly ICurrentUserService _currentUser;

    public SzController(
        ISzService sz,
        ISzExecutionService execution,
        DelosferaDbContext db,
        IDocumentService documents,
        ICurrentUserService currentUser)
    {
        _sz = sz;
        _execution = execution;
        _db = db;
        _documents = documents;
        _currentUser = currentUser;
    }

    /// <summary>Реестр СЗ с фильтрами. Пустой список статусов — неархивные записки.</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SzSearchRequest request) =>
        Ok(await _sz.SearchAsync(request, _currentUser.UserId));

    /// <summary>Карточка служебной записки.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var sz = await _sz.GetAsync(id);
        return sz is null ? NotFound(new { message = "Служебная записка не найдена" }) : Ok(sz);
    }

    /// <summary>Журнал действий по записке (аудит единой карточки документа).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        var sz = await _sz.GetAsync(id);
        if (sz is null) return NotFound(new { message = "Служебная записка не найдена" });

        var entries = await _documents.GetAuditAsync(sz.DocumentId);
        return Ok(entries.Select(e => new { e.Id, e.At, e.Action, e.UserId, payload = e.PayloadJson }));
    }

    /// <summary>Создать черновик.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SzSaveRequest request)
    {
        try
        {
            return Ok(await _sz.CreateDraftAsync(request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Изменить черновик (или записку, вернувшуюся на доработку).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SzSaveRequest request)
    {
        try
        {
            return Ok(await _sz.UpdateDraftAsync(id, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Удалить черновик.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _sz.DeleteDraftAsync(id, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отправить записку на регистрацию.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        try
        {
            return Ok(await _sz.SubmitAsync(id, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Решение адресата по существу вопроса (поле «Кому»).
    /// Пишет только тот пользователь, который в этом поле указан.
    /// </summary>
    [HttpPost("{id:int}/addressee-decision")]
    public async Task<IActionResult> AddresseeDecision(int id, [FromBody] SzAddresseeDecisionRequest req)
    {
        try
        {
            var decided = await _sz.DecideAsAddresseeAsync(id, req.Decision, _currentUser.UserId);

            // Поручения выдаются тем же действием: решение адресата и есть резолюция,
            // по которой работа расходится исполнителям. Отдельным шагом её пришлось бы
            // вводить дважды.
            if (req.Assignments.Count > 0)
            {
                await _execution.ResolveAsync(
                    id,
                    new SzResolutionRequest {Text = req.Decision, Assignments = req.Assignments},
                    _currentUser.UserId);

                decided = (await _sz.GetAsync(id))!;
            }

            return Ok(decided);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public class ApproversRequest
    {
        public List<int> UserIds { get; set; } = [];

        /// <summary>Параллельное согласование; иначе — по очереди.</summary>
        public bool Parallel { get; set; }
    }

    /// <summary>Состав и порядок согласующих (до отправки записки).</summary>
    [HttpPut("{id:int}/approvers")]
    public async Task<IActionResult> SetApprovers(int id, [FromBody] ApproversRequest req)
    {
        try
        {
            return Ok(await _sz.SetApproversAsync(id, req.UserIds, req.Parallel, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public class RegisterRequest
    {
        /// <summary>Шаблон маршрута; если не задан — берётся из вида записки.</summary>
        public int? TemplateId { get; set; }
    }

    public class WithdrawRequest
    {
        public required string Reason { get; set; }
    }

    public class SubmitToBodyRequest
    {
        /// <summary>Орган, на который выносится вопрос. Пусто — снять отметку.</summary>
        public Meetings.Models.MeetingBody? Body { get; set; }

        /// <summary>Предлагаемая формулировка вопроса для повестки.</summary>
        public string? Question { get; set; }
    }

    /// <summary>
    /// Зарегистрировать записку: присвоить номер, дату, срок исполнения
    /// и запустить маршрут согласования (SZ-01).
    /// </summary>
    /// <summary>
    /// Вынести вопрос по записке на коллегиальный орган.
    ///
    /// Доступно тому, кому записка адресована, и только с правом на это:
    /// Председателю Правления и исполняющему его обязанности.
    /// </summary>
    [HttpPost("{id:int}/to-body")]
    [RequirePermission(PermissionCode.SubmitSzToBody)]
    public async Task<IActionResult> ToBody(int id, [FromBody] SzToBodyRequest request)
    {
        try
        {
            return Ok(await _sz.SubmitToBodyAsync(id, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new {message = ex.Message}); }
        catch (ArgumentException ex) { return BadRequest(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    [HttpPost("{id:int}/register")]
    [RequirePermission(PermissionCode.RegisterSz)]
    public async Task<IActionResult> Register(int id, [FromBody] RegisterRequest? req = null)
    {
        try
        {
            return Ok(await _sz.RegisterAsync(id, _currentUser.UserId, req?.TemplateId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отозвать записку с согласования с обоснованием — возвращается в черновик.</summary>
    /// <summary>
    /// Перевести записку в другой статус вручную — право администратора системы.
    /// Обоснование обязательно и попадает в журнал действий.
    /// </summary>
    [HttpPost("{id:int}/force-status")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> ForceStatus(int id, [FromBody] SzForceStatusRequest request)
    {
        try
        {
            return Ok(await _sz.ForceStatusAsync(
                id, request.StatusCode, request.Reason, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (ArgumentException ex) { return BadRequest(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    /// <summary>Статусы, доступные для ручного перевода.</summary>
    [HttpGet("statuses")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public IActionResult Statuses() => Ok(SzStatus.All);

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, [FromBody] WithdrawRequest req)
    {
        try
        {
            return Ok(await _sz.WithdrawAsync(id, req.Reason, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Поставить или снять отметку «вынести на коллегиальный орган».
    ///
    /// Это заявка, а не распоряжение: записка встаёт в очередь к секретарю органа,
    /// и он решает, включать ли её в повестку. Ставит автор записки или адресат —
    /// первый знает, что вопрос выходит за его полномочия, второй приходит к этому
    /// при вынесении решения.
    /// </summary>
    [HttpPost("{id:int}/submit-to-body")]
    public async Task<IActionResult> SubmitToBody(int id, [FromBody] SubmitToBodyRequest req)
    {
        var sz = await _db.SzDocuments
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sz is null) return NotFound(new { message = "Записка не найдена." });

        var userId = _currentUser.UserId;
        var isAuthor = sz.Document?.AuthorId == userId;
        var isAddressee = sz.AddresseeUserId == userId;

        if (!isAuthor && !isAddressee)
            return Forbid();

        // Уже в повестке — снимать и переставлять отметку поздно: вопрос заведён,
        // и решать его судьбу теперь секретарю через саму повестку.
        var inAgenda = await _db.AgendaItems.AnyAsync(a => a.SourceSzId == id);
        if (inAgenda)
            return Conflict(new { message = "Записка уже включена в повестку заседания." });

        if (req.Body is null)
        {
            sz.SubmitToBody = null;
            sz.SubmitToBodyQuestion = null;
            sz.SubmitToBodyRequestedAt = null;
            sz.SubmitToBodyRequestedByUserId = null;
        }
        else
        {
            sz.SubmitToBody = req.Body;
            sz.SubmitToBodyQuestion = string.IsNullOrWhiteSpace(req.Question)
                ? null
                : req.Question.Trim();
            sz.SubmitToBodyRequestedAt = DateTime.UtcNow;
            sz.SubmitToBodyRequestedByUserId = userId;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            body = sz.SubmitToBody?.ToString(),
            question = sz.SubmitToBodyQuestion,
            requestedAt = sz.SubmitToBodyRequestedAt,
        });
    }

    /// <summary>«СЗ, согласую я»: записки, ждущие резолюции текущего пользователя.</summary>
    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await _sz.InboxAsync(_currentUser.UserId, page, pageSize));

    /// <summary>Справочник видов СЗ.</summary>
    [HttpGet("kinds")]
    public async Task<IActionResult> Kinds() =>
        Ok(await _db.SzKinds.AsNoTracking()
            .Where(k => k.IsActive)
            .OrderBy(k => k.Id)
            .Select(k => new { k.Id, k.TitleRu, k.TitleEn, k.TitleKg, formKey = k.FormKey.ToString(), k.IsPaperByDefault, k.ExecutionDays })
            .ToListAsync());

    /// <summary>Справочник видов кадровых СЗ.</summary>
    /// <summary>
    /// Какие поля показывать для каждого вида кадровой записки.
    ///
    /// Схема отдаётся целиком, а не по одному виду: пользователь переключает вид
    /// прямо в карточке, и запрашивать форму на каждое переключение значило бы
    /// подвешивать интерфейс на сеть там, где данные уже в памяти.
    /// </summary>
    [HttpGet("hr-forms")]
    public IActionResult HrForms() =>
        Ok(Services.HrFormSchema.Forms.ToDictionary(
            x => x.Key,
            x => new
            {
                x.Value.AllowMultipleEmployees,
                x.Value.EmployeeMayBeExternal,
                fields = x.Value.Fields.Select(f => new
                {
                    f.Code, f.Label, f.Type, f.Required, f.PerEmployee, f.Options, f.Hint,
                }),
            }));

    [HttpGet("hr-kinds")]
    public async Task<IActionResult> HrKinds() =>
        Ok(await _db.SzHrKinds.AsNoTracking()
            .Where(k => k.IsActive)
            .OrderBy(k => k.Id)
            .Select(k => new { k.Id, k.TitleRu, k.TitleEn, k.TitleKg })
            .ToListAsync());

    /// <summary>Счётчики по статусам для табов реестра.</summary>
    [HttpGet("counters")]
    public async Task<IActionResult> Counters()
    {
        var userId = _currentUser.UserId;

        var rows = await _db.SzDocuments.AsNoTracking()
            .Include(x => x.Document)
            .Where(x => x.Document!.StatusCode != SzStatus.Draft || x.Document!.AuthorId == userId)
            .GroupBy(x => x.Document!.StatusCode)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        var byStatus = rows.ToDictionary(r => r.status, r => r.count);

        return Ok(new
        {
            all = SzStatus.Active.Sum(s => byStatus.TryGetValue(s, out var c) ? c : 0),
            drafts = byStatus.TryGetValue(SzStatus.Draft, out var d) ? d : 0,
            pendingRegistration = byStatus.TryGetValue(SzStatus.PendingRegistration, out var p) ? p : 0,
            archived = byStatus.TryGetValue(SzStatus.Archived, out var a) ? a : 0,
            byStatus
        });
    }
}
