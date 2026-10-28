using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Controllers;

/// <summary>
/// Заявки на замещение (КСЗ-В9). Инициатор оформляет заявку с комиссией приёма-передачи,
/// УЧР исполняет (приказ на время замещения).
/// </summary>
[ApiController]
[Authorize]
[Route("api/substitution-requests")]
[Tags("Заявки на замещение")]
public class SubstitutionController : ControllerBase
{
    private readonly ISubstitutionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubstitutionStatisticsService _statistics;
    private readonly ISubstitutionSigningService _signing;

    public SubstitutionController(
        ISubstitutionService service, ICurrentUserService currentUser,
        ISubstitutionStatisticsService statistics, ISubstitutionSigningService signing)
    {
        _service = service;
        _currentUser = currentUser;
        _statistics = statistics;
        _signing = signing;
    }

    /// <summary>
    /// «Исполнено (приказ оформлен)» отмечает кадровик УЧР этой заявки (этап «УЧР» её маршрута:
    /// по ГО или по филиалам), тот, кто его сейчас замещает, и администратор — как технический
    /// запасной вариант. Раньше кнопку видели все кадровики и их замещающие по любой заявке.
    /// </summary>
    private async Task<bool> CanExecuteAsync(int id, CancellationToken ct) =>
        IsAdmin || await _service.IsResponsibleHrAsync(id, _currentUser.UserId, ct);

    /// <summary>
    /// Реестр заявок; mineOnly — только свои. УЧР видит все. absentUserId / substituteUserId /
    /// authorUserId — отсутствующий, замещающий и автор заявки; periodFrom / periodTo (yyyy-MM-dd) — пересечение с периодом замещения.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query, [FromQuery] string? status, [FromQuery] bool mineOnly = false,
        [FromQuery] int? absentUserId = null, [FromQuery] int? substituteUserId = null,
        [FromQuery] int? authorUserId = null,
        [FromQuery] DateOnly? periodFrom = null, [FromQuery] DateOnly? periodTo = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        // Кто не ведёт кадровые СЗ, видит свои заявки и те, где он согласующий:
        // иначе директор филиала не находил заявку, пришедшую ему на согласование.
        var participantOnly = !await CanManageAsync(ct);
        return Ok(await _service.SearchAsync(query, status, mineOnly, _currentUser.UserId, page, pageSize,
            participantOnly, absentUserId, substituteUserId, periodFrom, periodTo, authorUserId, ct));
    }

    /// <summary>
    /// Полный реестр заявок и ведение их как УЧР (правка после Операционного управления,
    /// переназначение этапов, «Исполнено», статистика): кадровики УЧР из настроек маршрута (и их
    /// замещающие) и администратор системы. Раньше это давало право «Видеть все СЗ», которое есть
    /// у Председателя, Правления, делопроизводства и руководителей, — и паспорт, ИНН, адреса
    /// замещающих видели все они.
    /// </summary>
    private async Task<bool> CanManageAsync(CancellationToken ct) =>
        _currentUser.HasPermission(PermissionCode.ManageSystemSettings)
        || await _service.IsHrManagerAsync(_currentUser.UserId, ct);

    private bool IsAdmin => _currentUser.HasPermission(PermissionCode.ManageSystemSettings);

    /// <summary>Корректировать заявку на согласовании/подписании: право или УЧР (CanManage).</summary>
    private async Task<bool> CanCorrectAsync(CancellationToken ct) =>
        _currentUser.HasPermission(PermissionCode.CorrectSubstitution) || await CanManageAsync(ct);

    /// <summary>Отозвать чужую заявку: администратор или право WithdrawAnySubstitution.</summary>
    private bool CanWithdrawAny =>
        IsAdmin || _currentUser.HasPermission(PermissionCode.WithdrawAnySubstitution);

    /// <summary>
    /// Карточка с правами текущего пользователя: что ему показывать (кнопки «Отозвать»,
    /// «Удалить», «Исполнено», правка УЧР) решает сервер, а не догадка клиента по ролям.
    /// </summary>
    private async Task<IActionResult> CardAsync(SubstitutionDetails d, CancellationToken ct)
    {
        var manage = await CanManageAsync(ct);
        var isInitiator = d.InitiatorUserId == _currentUser.UserId;
        // Кого пользователь замещает: по этому списку карточка показывает ему кнопки
        // согласования этапа отсутствующего (GEN-14).
        d.ActingForUserIds = await _service.ActingForUserIdsAsync(_currentUser.UserId, ct);
        d.Access = new SubstitutionAccessDto
        {
            CanManage = manage,
            IsInitiator = isInitiator,
            CanWithdraw = (isInitiator || CanWithdrawAny) && d.Status is "OnApproval" or "OnExecution",
            CanCorrect = d.Status is "OnApproval" or "OnExecution" && await CanCorrectAsync(ct),
            CanDelete = (isInitiator || IsAdmin) && d.Status == "Draft",
            CanExecute = d.Status == "OnExecution" && await CanExecuteAsync(d.Id, ct),
            CanEditBankSigners = d.Status != "Executed" && await CanCorrectAsync(ct),
        };
        return Ok(d);
    }

    /// <summary>Ведёт ли текущий пользователь заявки как УЧР — для реестра и статистики на клиенте.</summary>
    [HttpGet("access")]
    public async Task<IActionResult> Access(CancellationToken ct) =>
        Ok(new { canManage = await CanManageAsync(ct) });

    /// <summary>
    /// Закрыть чужую заявку: в карточке паспорт, ИНН и адреса замещающего, а печатные
    /// формы — приказ и договор о материальной ответственности. Раньше и то и другое
    /// отдавалось любому вошедшему по одному {id}. null — можно.
    /// </summary>
    private async Task<IActionResult?> DenyIfHiddenAsync(int id, CancellationToken ct)
    {
        if (await _service.CanViewAsync(id, _currentUser.UserId, await CanManageAsync(ct), ct)) return null;

        // Несуществующую и чужую заявку не различаем по коду ответа — иначе перебором
        // id можно узнать, какие заявки есть. Держателю полного реестра несуществующая
        // заявка по-прежнему отвечает 404 из самого метода.
        return StatusCode(403, new { message = "Нет доступа к этой заявке на замещение" });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (await DenyIfHiddenAsync(id, ct) is { } denied) return denied;

        var d = await _service.GetAsync(id, ct);
        if (d is null) return NotFound(new { message = "Заявка на замещение не найдена" });

        return await CardAsync(d, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SubstitutionSaveRequest request, CancellationToken ct)
    {
        try { return await CardAsync(await _service.CreateAsync(request, _currentUser.UserId, ct), ct); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SubstitutionSaveRequest request, CancellationToken ct)
    {
        try { return await CardAsync(await _service.UpdateAsync(id, request, _currentUser.UserId, await CanManageAsync(ct), ct, await CanCorrectAsync(ct)), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отправить заявку — присваивается номер, уходит в УЧР на исполнение.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        try { return await CardAsync(await _service.SubmitAsync(id, _currentUser.UserId, IsAdmin, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public record DecisionRequest(string? Comment);

    /// <summary>Согласовать текущий этап (директор филиала / Опер. управление / УЧР).</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] DecisionRequest? req, CancellationToken ct)
    {
        try { return await CardAsync(await _service.ApproveAsync(id, _currentUser.UserId, req?.Comment, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Отклонить заявку на текущем этапе — возвращается инициатору.</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] DecisionRequest? req, CancellationToken ct)
    {
        try { return await CardAsync(await _service.RejectAsync(id, _currentUser.UserId, req?.Comment, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Исполнить заявку: приказ оформлен по-настоящему — подписан (в системе или на бумаге),
    /// зарегистрирован, сотрудник допущен к замещению. Можно в любой момент этапа «На подписании» —
    /// в том числе без подписей ЭЦП, если документы подписаны на бумаге. Отмечает кадровик УЧР
    /// этой заявки, его замещающий или администратор.
    /// </summary>
    [HttpPost("{id:int}/execute")]
    public async Task<IActionResult> Execute(int id, CancellationToken ct)
    {
        if (!await CanExecuteAsync(id, ct))
            return StatusCode(403, new { message = "Отметить исполнение может кадровик УЧР этой заявки, его замещающий или администратор" });

        try { return await CardAsync(await _service.ExecuteAsync(id, _currentUser.UserId, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    public record ReassignRequest(int UserId, string? Reason);

    /// <summary>
    /// Переназначить непройденный этап действующего маршрута другому сотруднику — когда
    /// согласующий заблокирован, уволен или надолго отсутствует без замещения. УЧР и администратор.
    /// </summary>
    [HttpPost("{id:int}/approvals/{approvalId:int}/reassign")]
    public async Task<IActionResult> Reassign(int id, int approvalId, [FromBody] ReassignRequest req, CancellationToken ct)
    {
        if (!await CanManageAsync(ct))
            return StatusCode(403, new { message = "Переназначать этапы могут УЧР и администратор" });

        try { return await CardAsync(await _service.ReassignStepAsync(id, approvalId, req.UserId, req.Reason ?? "", _currentUser.UserId, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        try { return await CardAsync(await _service.WithdrawAsync(id, _currentUser.UserId, CanWithdrawAny, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Печатная форма: form = order (приказ) или liability (договор МО), как «Скачать / печать» у СЗ:
    /// signed=true — «С подписями ЭЦП»: действующая версия с листом подписей ЭЦП (неактуальные
    /// подписи — автосогласованием); signed=false — «Для подписи от руки»: тот же зафиксированный
    /// файл без листа. Пока документы не зафиксированы (черновик) — форма по текущим данным.
    /// </summary>
    /// <summary>
    /// Дополнительные подписанты договора МО от имени Банка (кроме основного — Председателя
    /// Правления из настроек маршрута УЧР). Ведут делопроизводство (CorrectSubstitution) и УЧР.
    /// </summary>
    [HttpPut("{id:int}/bank-signers")]
    public async Task<IActionResult> SetBankSigners(int id, [FromBody] SubstitutionBankSignersRequest request, CancellationToken ct)
    {
        if (await DenyIfHiddenAsync(id, ct) is { } denied) return denied;
        if (!await CanCorrectAsync(ct))
            return StatusCode(403, new { message = "Подписантов договора от Банка добавляют делопроизводство и УЧР" });
        try { return await CardAsync(await _service.SetBankSignersAsync(id, request.UserIds, _currentUser.UserId, ct), ct); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:int}/print/{form}")]
    public async Task<IActionResult> Print(int id, string form, [FromQuery] bool signed = false, CancellationToken ct = default)
    {
        if (await DenyIfHiddenAsync(id, ct) is { } denied) return denied;

        try
        {
            if (form is "order" or "liability")
            {
                var kind = form == "order"
                    ? Models.SubstitutionDocumentKind.Order
                    : Models.SubstitutionDocumentKind.Liability;
                if (await _signing.DownloadCurrentAsync(id, kind, signed, ct) is { } current)
                    return File(current.Bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", current.FileName);
            }

            var (bytes, name) = await _service.PrintAsync(id, form, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, _currentUser.UserId, _currentUser.HasPermission(PermissionCode.ManageSystemSettings), ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // ── ЗМ-SLA: норматив срока согласования, статистика, выгрузка ──────────────

    /// <summary>Норматив срока согласования (рабочих дней на этап).</summary>
    [HttpGet("sla")]
    public async Task<IActionResult> GetSla(CancellationToken ct) =>
        Ok(new { approvalStepSlaDays = await _service.GetSlaDaysAsync(ct) });

    /// <summary>Задать норматив срока согласования. Ведёт администратор.</summary>
    [HttpPut("sla")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> SetSla([FromBody] SubstitutionSlaRequest request, CancellationToken ct)
    {
        try { return Ok(new { approvalStepSlaDays = await _service.SetSlaDaysAsync(request.ApprovalStepSlaDays, ct) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Статистика по заявкам на замещение (в работе / просрочено / исполнено).</summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (!await CanManageAsync(ct))
            return StatusCode(403, new { message = "Статистика заявок на замещение доступна УЧР и администратору" });
        return Ok(await _statistics.GetAsync(new SubstitutionStatisticsFilter { From = from, To = to }, ct));
    }

    /// <summary>Выгрузка статистики в Excel.</summary>
    [HttpGet("statistics/export")]
    public async Task<IActionResult> StatisticsExport(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (!await CanManageAsync(ct))
            return StatusCode(403, new { message = "Статистика заявок на замещение доступна УЧР и администратору" });
        var bytes = await _statistics.ExportAsync(new SubstitutionStatisticsFilter { From = from, To = to }, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "substitutions-statistics.xlsx");
    }
}
