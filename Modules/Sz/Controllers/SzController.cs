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
    private readonly ISzApproverPreviewService _approverPreview;

    public SzController(
        ISzService sz,
        ISzExecutionService execution,
        DelosferaDbContext db,
        IDocumentService documents,
        ICurrentUserService currentUser,
        ISzApproverPreviewService approverPreview)
    {
        _sz = sz;
        _execution = execution;
        _db = db;
        _documents = documents;
        _currentUser = currentUser;
        _approverPreview = approverPreview;
    }

    /// <summary>Реестр СЗ с фильтрами. Пустой список статусов — неархивные записки.</summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SzSearchRequest request) =>
        Ok(await _sz.SearchAsync(request, _currentUser.UserId));

    /// <summary>Тот же реестр целиком, книгой Excel. Видимость — как у поиска.</summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] SzSearchRequest request)
    {
        var bytes = await _sz.ExportAsync(request, _currentUser.UserId);
        var stamp = delosfera_server.Common.Services.WorkingCalendar.Today.ToString("dd.MM.yyyy");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Реестр СЗ {stamp}.xlsx");
    }

    /// <summary>
    /// Закрыть доступ к чужой записке: карточку, журнал и путь записки отдаём только
    /// тем, кому она видна в реестре (<see cref="SzVisibility"/>). null — можно.
    /// </summary>
    private async Task<IActionResult?> DenyIfHiddenAsync(int id)
    {
        if (!await _db.SzDocuments.AnyAsync(x => x.Id == id))
            return NotFound(new { message = "Служебная записка не найдена" });

        if (!await _sz.CanViewAsync(id, _currentUser.UserId))
            return StatusCode(403, new { message = "Нет доступа к этой служебной записке" });

        return null;
    }

    /// <summary>Карточка служебной записки.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (await DenyIfHiddenAsync(id) is { } denied) return denied;

        var sz = await _sz.GetAsync(id);
        if (sz is null) return NotFound(new { message = "Служебная записка не найдена" });

        // Кого пользователь замещает: по этому списку карточка показывает ему действия
        // за отсутствующего (GEN-14).
        sz.ActingForUserIds = await delosfera_server.Modules.Users.Services.ActingFor.AbsentUserIdsAsync(
            _db, _currentUser.UserId, delosfera_server.Common.Services.WorkingCalendar.Today);

        return Ok(sz);
    }

    /// <summary>Журнал действий по записке (аудит единой карточки документа).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        if (await DenyIfHiddenAsync(id) is { } denied) return denied;

        var sz = await _sz.GetAsync(id);
        if (sz is null) return NotFound(new { message = "Служебная записка не найдена" });

        var entries = await _documents.GetAuditAsync(sz.DocumentId);
        return Ok(entries.Select(e => new { e.Id, e.At, e.Action, e.UserId, payload = e.PayloadJson }));
    }

    /// <summary>
    /// Возможные дубликаты создаваемой записки (СК-5): похожие записки текущего автора
    /// по виду и теме за последние 30 дней. excludeId — id текущего черновика.
    /// </summary>
    [HttpGet("duplicates")]
    public async Task<IActionResult> Duplicates(
        [FromQuery] int kindId, [FromQuery] string? title, [FromQuery] int? excludeId) =>
        Ok(await _sz.FindDuplicatesAsync(kindId, title, _currentUser.UserId, excludeId));

    /// <summary>Путь записки по статусам с длительностью каждого этапа (СЗ-8).</summary>
    [HttpGet("{id:int}/trace")]
    public async Task<IActionResult> Trace(int id)
    {
        if (await DenyIfHiddenAsync(id) is { } denied) return denied;

        var trace = await _sz.GetTraceAsync(id);
        return trace is null ? NotFound(new { message = "Служебная записка не найдена" }) : Ok(trace);
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
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Кадровик УЧР проставляет «в бюджете/вне» и позицию плана закупок по СЗ на обучение (КСЗ-08).
    /// </summary>
    [HttpPost("{id:int}/training-budget")]
    [RequirePermission(PermissionCode.ViewAllSz)]
    public async Task<IActionResult> SetTrainingBudget(int id, [FromBody] SzTrainingBudgetRequest request)
    {
        try
        {
            return Ok(await _sz.SetTrainingBudgetAsync(id, request.HasBudget, request.PlanItemId, _currentUser.UserId));
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
            // Решение и поручения — одно действие: если поручения не прошли проверку
            // (нет исполнителя, исполнитель указан дважды), решение тоже не должно
            // сохраниться. Иначе оно записывалось, а повторная попытка получала
            // «Решение уже вынесено», и выдать поручения тем же действием было нельзя.
            await using var tx = await _db.Database.BeginTransactionAsync();

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

            await tx.CommitAsync();
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

    public class RouteParticipantRequest
    {
        public int UserId { get; set; }
        /// <summary>Поставить перед этим этапом; не задан — перед подписанием, иначе в конец.</summary>
        public int? BeforeStepOrder { get; set; }
    }

    public class ReplaceParticipantRequest
    {
        public int UserId { get; set; }
    }

    /// <summary>Добавить согласующего в идущий маршрут (автор или право CorrectSzOnApproval).</summary>
    [HttpPost("{id:int}/route/participants")]
    public async Task<IActionResult> AddRouteParticipant(int id, [FromBody] RouteParticipantRequest req)
    {
        try
        {
            return Ok(await _sz.AddRouteParticipantAsync(id, req.UserId, req.BeforeStepOrder, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Убрать из маршрута участника, ещё не вынесшего решение (автор или право CorrectSzOnApproval).</summary>
    [HttpDelete("{id:int}/route/participants/{participantId:int}")]
    public async Task<IActionResult> RemoveRouteParticipant(int id, int participantId)
    {
        try
        {
            return Ok(await _sz.RemoveRouteParticipantAsync(id, participantId, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Заменить участника маршрута, ещё не вынесшего решение (согласующего или подписанта),
    /// другим сотрудником — без отзыва и нового круга (автор или право CorrectSzOnApproval).
    /// </summary>
    [HttpPut("{id:int}/route/participants/{participantId:int}")]
    public async Task<IActionResult> ReplaceRouteParticipant(int id, int participantId, [FromBody] ReplaceParticipantRequest req)
    {
        try
        {
            return Ok(await _sz.ReplaceRouteParticipantAsync(id, participantId, req.UserId, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
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

    /// <summary>Снять записку с вынесения на коллегиальный орган (пока не в повестке).</summary>
    [HttpPost("{id:int}/to-body/withdraw")]
    [RequirePermission(PermissionCode.SubmitSzToBody)]
    public async Task<IActionResult> WithdrawFromBody(int id)
    {
        try
        {
            return Ok(await _sz.WithdrawFromBodyAsync(id, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new {message = ex.Message}); }
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

    /// <summary>Согласующие из шаблона выбранного вида — для автоподстановки в форму.</summary>
    /// <remarks>Возвращает список тех, кого подставит движок при отправке (по шаблону
    /// вида/подразделения), чтобы автор видел его заранее и мог поправить. Пусто —
    /// шаблон не настроен, согласующих выбирают вручную.</remarks>
    [HttpGet("kinds/{kindId:int}/preview-approvers")]
    public async Task<IActionResult> PreviewApprovers(int kindId, [FromQuery] int? correspondentUnitId) =>
        Ok(await _approverPreview.PreviewAsync(kindId, correspondentUnitId));

    /// <summary>Справочник видов кадровых СЗ.</summary>
    /// <summary>
    /// Какие поля показывать для каждого вида кадровой записки.
    ///
    /// Схема отдаётся целиком, а не по одному виду: пользователь переключает вид
    /// прямо в карточке, и запрашивать форму на каждое переключение значило бы
    /// подвешивать интерфейс на сеть там, где данные уже в памяти.
    /// </summary>
    /// <summary>Кто кадровик УЧР по областям (ГО / филиалы) для маршрута кадровых СЗ (КСЗ-12).</summary>
    [HttpGet("hr-routing")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> GetHrRouting()
    {
        var s = await _db.HrRoutingSettings.AsNoTracking().FirstOrDefaultAsync();
        return Ok(new HrRoutingSettingsDto
        {
            HeadOfficeHrUserId = s?.HeadOfficeHrUserId,
            BranchHrUserId = s?.BranchHrUserId,
            OperationsApproverUserId = s?.OperationsApproverUserId,
            OperationsFallbackUserId = s?.OperationsFallbackUserId,
            HeadOfficeSignerUserId = s?.HeadOfficeSignerUserId,
            OrderVisaUserIds = s?.OrderVisaUserIds.ToList() ?? [],
        });
    }

    /// <summary>Назначить кадровиков УЧР по областям. Ведёт УЧР без участия разработчика (КСЗ-12).</summary>
    [HttpPut("hr-routing")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> SetHrRouting([FromBody] HrRoutingSettingsDto req)
    {
        var now = DateTime.UtcNow;
        var s = await _db.HrRoutingSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new Models.HrRoutingSettings { CreatedAt = now };
            _db.HrRoutingSettings.Add(s);
        }
        s.HeadOfficeHrUserId = req.HeadOfficeHrUserId;
        s.BranchHrUserId = req.BranchHrUserId;
        s.OperationsApproverUserId = req.OperationsApproverUserId;
        s.OperationsFallbackUserId = req.OperationsFallbackUserId;
        s.HeadOfficeSignerUserId = req.HeadOfficeSignerUserId;
        // Старый клиент поле не присылает — список визирующих тогда не трогаем.
        if (req.OrderVisaUserIds is { } visas)
            s.OrderVisaUserIds = visas.Where(id => id > 0).Distinct().ToArray();
        s.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return Ok(new HrRoutingSettingsDto
        {
            HeadOfficeHrUserId = s.HeadOfficeHrUserId,
            BranchHrUserId = s.BranchHrUserId,
            OperationsApproverUserId = s.OperationsApproverUserId,
            OperationsFallbackUserId = s.OperationsFallbackUserId,
            HeadOfficeSignerUserId = s.HeadOfficeSignerUserId,
            OrderVisaUserIds = s.OrderVisaUserIds.ToList(),
        });
    }

    [HttpGet("hr-forms")]
    public IActionResult HrForms() =>
        Ok(Services.HrFormSchema.Forms.ToDictionary(
            x => x.Key,
            x => new
            {
                // Ключ словаря остаётся названием вида — старый клиент работает как раньше;
                // новый ищет схему по hrKindId и не ломается при переименовании вида.
                x.Value.HrKindId,
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
    public async Task<IActionResult> Counters([FromServices] ISzPaperService paper)
    {
        var userId = _currentUser.UserId;

        // По статусам считаем только видимые пользователю записки — по тому же правилу,
        // что и списки вкладок. Раньше счёт шёл по всему банку: в кружке «Архив (340)»,
        // а в списке три строки.
        var byStatus = await _sz.CountByStatusAsync(userId);

        return Ok(new
        {
            all = SzStatus.Active.Sum(s => byStatus.TryGetValue(s, out var c) ? c : 0),
            // «Черновики» — свои черновики и отозванные (отозванная возвращается автору
            // на правку): тот же фильтр, что у самой вкладки.
            drafts = await _sz.CountAsync(new SzSearchRequest
            {
                MineOnly = true, Statuses = [SzStatus.Draft, SzStatus.Withdrawn],
            }, userId),
            // «Черновики других пользователей» — подвкладка администратора системы; остальным
            // счётчик не отдаём (null), как и вкладку.
            othersDrafts = _currentUser.HasPermission(PermissionCode.ManageSystemSettings)
                ? await _sz.CountAsync(new SzSearchRequest
                {
                    OthersOnly = true, Statuses = [SzStatus.Draft, SzStatus.Withdrawn],
                }, userId)
                : (int?)null,
            pendingRegistration = byStatus.TryGetValue(SzStatus.PendingRegistration, out var p) ? p : 0,
            archived = byStatus.TryGetValue(SzStatus.Archived, out var a) ? a : 0,
            byStatus,
            // Счётчики особых вкладок — по тем же фильтрам, что и сами списки, чтобы
            // число в кружке совпадало со строками. Каждый — один COUNT, строки не выбираются.
            mine = await _sz.CountAsync(new SzSearchRequest { MineOnly = true }, userId),
            favorites = await _sz.CountAsync(new SzSearchRequest { FavoritesOnly = true }, userId),
            inbox = await _sz.InboxCountAsync(userId),
            assignments = await _execution.CountMyAsync(userId),
            originals = await CountOriginalsAsync(paper)
        });
    }

    /// <summary>
    /// Реестр оригиналов доступен только делопроизводству: остальным счётчик
    /// не отдаём (null), а не роняем весь запрос счётчиков.
    /// </summary>
    private static async Task<int?> CountOriginalsAsync(ISzPaperService paper)
    {
        try
        {
            return await paper.CountOutstandingAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
