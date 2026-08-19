using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Параметры контура закупок: пороги Положения и ссылка на само Положение.
///
/// Значения меняются — баланс пересчитывается ежеквартально, пороги правятся
/// решением Правления, — и до сих пор их можно было изменить только запросом к
/// базе напрямую. Это означало, что администратору некуда зайти, а всякая правка
/// проходила мимо журнала.
/// </summary>
[ApiController]
[Authorize]
[Route("api/procurement/parameters")]
[Tags("Закупки")]
public class ProcurementParameterController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public ProcurementParameterController(
        DelosferaDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _db.ProcurementParameters
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.TitleRu,
                p.Value,
                p.Unit,
                p.SourceNote,
                p.UpdatedAt,
            })
            .ToListAsync(ct));

    /// <summary>Изменить значение параметра и основание, по которому оно принято.</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update(int id, [FromBody] ParameterUpdateRequest request, CancellationToken ct)
    {
        var parameter = await _db.ProcurementParameters.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parameter is null) return NotFound(new {message = "Параметр не найден"});

        if (request.Value < 0)
            return BadRequest(new {message = "Значение параметра не может быть отрицательным"});

        var было = parameter.Value;

        parameter.Value = request.Value;
        parameter.SourceNote = string.IsNullOrWhiteSpace(request.SourceNote)
            ? parameter.SourceNote
            : request.SourceNote.Trim();

        await _db.SaveChangesAsync(ct);

        // Порог закупки — величина, от которой зависит способ и орган утверждения.
        // Кто и когда её сдвинул, должно быть видно в журнале.
        await _audit.LogAsync("ProcurementParameter", parameter.Id, "Updated", _currentUser.UserId, new
        {
            parameter.Code,
            было,
            стало = parameter.Value,
            parameter.SourceNote,
        });

        return Ok(new {parameter.Id, parameter.Code, parameter.TitleRu, parameter.Value, parameter.Unit, parameter.SourceNote});
    }
}

public class ParameterUpdateRequest
{
    public decimal Value { get; set; }

    /// <summary>Основание: решение Правления, дата отчётности — чтобы при приёмке было видно, откуда взято.</summary>
    public string? SourceNote { get; set; }
}
