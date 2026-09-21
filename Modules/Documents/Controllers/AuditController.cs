using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Controllers;

/// <summary>Целостность журнала аудита (AUD-1): проверка хеш-цепи и разовый бэкфилл легаси.</summary>
[ApiController]
[Authorize]
[Route("api/audit")]
[Tags("Аудит")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    /// <summary>Проверить целостность хеш-цепи аудита. Возвращает Id первой нарушенной записи, если цепь разорвана.</summary>
    [HttpGet("integrity")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Integrity(CancellationToken ct) =>
        Ok(await _audit.VerifyChainAsync(ct));

    /// <summary>Достроить цепь по легаси-записям без хеша (однократно после внедрения AUD-1).</summary>
    [HttpPost("backfill")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Backfill(CancellationToken ct) =>
        Ok(new { filled = await _audit.BackfillChainAsync(ct) });
}
