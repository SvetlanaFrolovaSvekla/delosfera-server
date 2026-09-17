using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Controllers;

/// <summary>
/// Настройка нумерации заявок на замещение: формат и текущий счётчик.
///
/// Счётчик сквозной (scope Global, без сброса по годам): заявки идут HR-1, HR-2, …
/// Формат и текущее значение правит администратор из настроек системы — например,
/// чтобы сменить префикс или продолжить нумерацию с нужного числа.
/// </summary>
[ApiController]
[Authorize]
[Route("api/substitution-numbering")]
[Tags("Нумерация замещений")]
public class SubstitutionNumberingController : ControllerBase
{
    // Ключ нумератора заявок на замещение — единая сквозная последовательность.
    private const DocumentType Type = DocumentType.Custom;
    private const string Scope = "Substitution";
    private const string ScopeKey = "Global";
    private const string DefaultPattern = "HR-{seq}";

    private readonly DelosferaDbContext _db;

    public SubstitutionNumberingController(DelosferaDbContext db) => _db = db;

    public record NumberingDto(string Pattern, int NextSeq);
    public record NumberingRequest(string Pattern, int NextSeq);

    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<NumberingDto>> Get(CancellationToken ct)
    {
        var num = await _db.Numerators.AsNoTracking().FirstOrDefaultAsync(
            n => n.DocumentType == Type && n.Scope == Scope && n.ScopeKey == ScopeKey, ct);
        return Ok(new NumberingDto(num?.Pattern ?? DefaultPattern, num?.NextSeq ?? 1));
    }

    [HttpPut]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<NumberingDto>> Save([FromBody] NumberingRequest req, CancellationToken ct)
    {
        var pattern = string.IsNullOrWhiteSpace(req.Pattern) ? DefaultPattern : req.Pattern.Trim();
        if (!pattern.Contains("{seq}"))
            return BadRequest(new { message = "Формат должен содержать {seq} — место порядкового номера" });
        if (req.NextSeq < 1)
            return BadRequest(new { message = "Счётчик не может быть меньше 1" });

        var num = await _db.Numerators.FirstOrDefaultAsync(
            n => n.DocumentType == Type && n.Scope == Scope && n.ScopeKey == ScopeKey, ct);
        if (num is null)
        {
            num = new Numerator { DocumentType = Type, Scope = Scope, ScopeKey = ScopeKey, Pattern = pattern, NextSeq = req.NextSeq };
            _db.Numerators.Add(num);
        }
        else
        {
            num.Pattern = pattern;
            num.NextSeq = req.NextSeq;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new NumberingDto(num.Pattern, num.NextSeq));
    }
}
