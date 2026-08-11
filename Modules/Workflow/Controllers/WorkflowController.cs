using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Workflow.Controllers;

/// <summary>
/// Движок согласования: шаблоны маршрутов, экземпляры, резолюции (TID-01..14).
/// </summary>
[ApiController]
[Route("api/workflow")]
[Tags("Согласование")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IRouteEngine _engine;
    private readonly ICurrentUserService _currentUser;

    public WorkflowController(DelosferaDbContext db, IRouteEngine engine, ICurrentUserService currentUser)
    {
        _db = db;
        _engine = engine;
        _currentUser = currentUser;
    }

    /// <summary>Список шаблонов маршрутов (опц. фильтр по типу документа) — для выбора при отправке.</summary>
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates([FromQuery] string? documentType = null)
    {
        var q = _db.RouteTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(documentType) &&
            Enum.TryParse<delosfera_server.Modules.Documents.Models.DocumentType>(documentType, out var dt))
            q = q.Where(t => t.DocumentType == dt);

        var items = await q
            .Select(t => new { t.Id, t.Name, documentType = t.DocumentType.ToString(), t.IsGlobalRule })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Создать шаблон маршрута (справочник, TID-06).</summary>
    [HttpPost("templates")]
    public async Task<ActionResult<int>> CreateTemplate([FromBody] CreateRouteTemplateRequest req)
    {
        var tpl = new RouteTemplate
        {
            DocumentType = req.DocumentType,
            Name = req.Name,
            IsGlobalRule = req.IsGlobalRule,
            Steps = req.Steps.Select(s => new RouteTemplateStep
            {
                Order = s.Order,
                Mode = s.Mode,
                Kind = s.Kind,
                IsFinalMethodology = s.IsFinalMethodology,
                TimeNormHours = s.TimeNormHours,
                Participants = s.Participants.Select(p => new RouteTemplateParticipant
                {
                    UserId = p.UserId, UnitId = p.UnitId, RoleRef = p.RoleRef, Required = p.Required
                }).ToList()
            }).ToList()
        };
        _db.RouteTemplates.Add(tpl);
        await _db.SaveChangesAsync();
        return Ok(tpl.Id);
    }

    /// <summary>Создать экземпляр маршрута документа из шаблона.</summary>
    [HttpPost("instances/from-template")]
    public async Task<ActionResult<RouteInstanceResponse>> Instantiate([FromBody] InstantiateRequest req)
    {
        var inst = await _engine.InstantiateFromTemplateAsync(req.DocumentId, req.TemplateId);
        return Ok(await LoadResponse(inst.Id));
    }

    /// <summary>Запустить маршрут.</summary>
    [HttpPost("instances/{id:int}/start")]
    public async Task<ActionResult<RouteInstanceResponse>> Start(int id)
    {
        try
        {
            await _engine.StartAsync(id, _currentUser.UserId);
            return Ok(await LoadResponse(id));
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Резолюция участника.</summary>
    [HttpPost("participants/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveRequest req)
    {
        try
        {
            await _engine.ResolveAsync(id, req.Type, req.Comment, _currentUser.UserId, req.SignatureId);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Подтвердить устранение замечания (строгий режим).</summary>
    [HttpPost("remarks/{id:int}/confirm")]
    public async Task<IActionResult> ConfirmRemark(int id)
    {
        await _engine.ConfirmRemarkResolvedAsync(id, _currentUser.UserId);
        return NoContent();
    }

    /// <summary>Состояние экземпляра маршрута.</summary>
    [HttpGet("instances/{id:int}")]
    public async Task<ActionResult<RouteInstanceResponse>> Get(int id)
    {
        var resp = await LoadResponse(id);
        return resp is null ? NotFound() : Ok(resp);
    }

    private async Task<RouteInstanceResponse?> LoadResponse(int id)
    {
        var inst = await _db.RouteInstances.AsNoTracking()
            .Include(i => i.Steps.OrderBy(s => s.Order)).ThenInclude(s => s.Participants)
                .ThenInclude(p => p.Resolution).ThenInclude(r => r!.Remarks)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inst is null) return null;

        // ФИО подтягиваем здесь: у участников согласования может не быть доступа
        // к справочнику пользователей, а лист согласования им виден.
        var userIds = inst.Steps.SelectMany(s => s.Participants)
            .Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return new RouteInstanceResponse
        {
            Id = inst.Id,
            DocumentId = inst.DocumentId,
            Status = inst.Status.ToString(),
            CurrentStepOrder = inst.CurrentStepOrder,
            Steps = inst.Steps.OrderBy(s => s.Order).Select(s => new StepResponse
            {
                Id = s.Id, Order = s.Order, Mode = s.Mode.ToString(), Kind = s.Kind.ToString(),
                IsFinalMethodology = s.IsFinalMethodology,
                Participants = s.Participants.OrderBy(p => p.Id).Select(p => new ParticipantResponse
                {
                    Id = p.Id, UserId = p.UserId, Required = p.Required, State = p.State.ToString(),
                    UserFullName = p.UserId != null && names.TryGetValue(p.UserId.Value, out var fio) ? fio : null,
                    Resolution = p.Resolution is null ? null : new ResolutionResponse
                    {
                        Type = p.Resolution.Type.ToString(),
                        Comment = p.Resolution.Comment,
                        Remarks = p.Resolution.Remarks.Select(rm => new RemarkResponse
                        {
                            Id = rm.Id, Text = rm.Text, State = rm.State.ToString()
                        }).ToList()
                    }
                }).ToList()
            }).ToList()
        };
    }
}
