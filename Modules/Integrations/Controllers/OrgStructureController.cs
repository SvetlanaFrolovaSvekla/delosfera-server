using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Security;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Integrations.OrgStructure;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Integrations.Controllers;

// ── Что уходит наружу ───────────────────────────────────────────────────────

public record OrgStructureSettingsResponse(
    bool Enabled,
    string PortalUrl,
    bool HasToken,
    int SyncIntervalMinutes,
    bool CreateMissingUnits,
    bool MatchByEmail);

public record OrgStructureSettingsRequest(
    bool Enabled,
    string PortalUrl,
    /// <summary>Пусто — оставить прежний. Токен показывают один раз, и вводить его заново нельзя.</summary>
    string? Token,
    int SyncIntervalMinutes,
    bool CreateMissingUnits,
    bool MatchByEmail);

public record OrgSyncRunResponse(
    long Id,
    DateTime StartedAt,
    DateTime? FinishedAt,
    string Outcome,
    string? StartedBy,
    int UnitsReceived,
    int UnitsCreated,
    int UnitsUpdated,
    int UnitsSkipped,
    int EmployeesReceived,
    int EmployeesMatched,
    int EmployeesUnmatched,
    int EmployeesUpdated,
    int EmployeesDeactivated,
    string? Error,
    string[] Notes);

/// <summary>
/// Оргструктура из портала: настройки связи, запуск вручную, история проходов.
///
/// Право то же, что и у прочих интеграций: это настройка системы, а не работа
/// с документами.
/// </summary>
[ApiController]
[Route("api/integrations/org-structure")]
[Authorize]
public class OrgStructureController(
    DelosferaDbContext db,
    IOrgSyncService sync,
    ISecretProtector protector,
    ICurrentUserService currentUser,
    IServiceProvider services) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<OrgStructureSettingsResponse>> GetSettings(CancellationToken ct)
    {
        var s = await db.OrgStructureSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        // Записи может не быть — связь ни разу не настраивали. Отдаём значения
        // по умолчанию, чтобы экран открывался, а не падал на пустоте.
        return Ok(s is null
            ? new OrgStructureSettingsResponse(false, "", false, 1440, true, true)
            : new OrgStructureSettingsResponse(
                s.Enabled, s.PortalUrl, !string.IsNullOrEmpty(s.TokenEncrypted),
                s.SyncIntervalMinutes, s.CreateMissingUnits, s.MatchByEmail));
    }

    [HttpPut("settings")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<OrgStructureSettingsResponse>> SaveSettings(
        [FromBody] OrgStructureSettingsRequest request, CancellationToken ct)
    {
        if (request.Enabled && string.IsNullOrWhiteSpace(request.PortalUrl))
            return BadRequest(new { message = "Укажите адрес портала." });

        if (!string.IsNullOrWhiteSpace(request.PortalUrl)
            && !Uri.TryCreate(request.PortalUrl, UriKind.Absolute, out var uri))
            return BadRequest(new { message = "Адрес портала должен быть полным, вместе с https://" });

        var now = DateTime.UtcNow;
        var s = await db.OrgStructureSettings.FirstOrDefaultAsync(ct);

        if (s is null)
        {
            s = new OrgStructureSettings { CreatedAt = now };
            db.OrgStructureSettings.Add(s);
        }

        s.Enabled = request.Enabled;
        s.PortalUrl = request.PortalUrl.Trim();
        s.CreateMissingUnits = request.CreateMissingUnits;
        s.MatchByEmail = request.MatchByEmail;

        // Ниже пяти минут не опускаем: портал просит не опрашивать его в цикле.
        s.SyncIntervalMinutes = Math.Max(request.SyncIntervalMinutes, 5);

        // Пустой токен означает «оставить прежний»: администратор правит адрес
        // или интервал, а токена у него на руках нет — его показывают один раз.
        if (!string.IsNullOrWhiteSpace(request.Token))
            s.TokenEncrypted = protector.Protect(request.Token.Trim());

        if (s.Enabled && string.IsNullOrEmpty(s.TokenEncrypted))
            return BadRequest(new { message = "Без токена синхронизацию включить нельзя." });

        s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return Ok(new OrgStructureSettingsResponse(
            s.Enabled, s.PortalUrl, !string.IsNullOrEmpty(s.TokenEncrypted),
            s.SyncIntervalMinutes, s.CreateMissingUnits, s.MatchByEmail));
    }

    /// <summary>Проверка связи: отвечает ли портал и принимает ли токен.</summary>
    [HttpPost("check")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Check([FromBody] OrgStructureSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var count = await sync.CheckConnectionAsync(request.PortalUrl, request.Token, ct);
            return Ok(new { ok = true, message = $"Портал отвечает. Подразделений видно: {count}." });
        }
        catch (PortalException e)
        {
            return Ok(new { ok = false, message = e.Message });
        }
    }

    /// <summary>
    /// Забрать структуру сейчас, не дожидаясь расписания.
    ///
    /// Запускает проход и сразу возвращает управление. Обход портала занимает
    /// минуты: справочник берётся страницами, а сотрудников в банке под четыре
    /// сотни. Обратный прокси столько не ждёт — он обрывал запрос, администратор
    /// видел ошибку, а проход при этом шёл дальше и заканчивался успешно.
    ///
    /// Ход виден в истории: она обновляется по ходу дела.
    /// </summary>
    [HttpPost("sync")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public IActionResult SyncNow()
    {
        var userId = currentUser.UserId;

        // Своя область служб: та, что у запроса, закроется вместе с ответом,
        // и проход остался бы с уничтоженным контекстом базы.
        _ = Task.Run(async () =>
        {
            using var scope = services.CreateScope();
            var run = scope.ServiceProvider.GetRequiredService<IOrgSyncService>();

            try
            {
                // Признак отмены не передаём: проход живёт дольше запроса,
                // и обрывать его нечему.
                await run.RunAsync(userId, CancellationToken.None);
            }
            catch (Exception e)
            {
                scope.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("OrgSync")
                    .LogError(e, "Проход синхронизации, запущенный вручную, прервался");
            }
        });

        return Accepted(new { message = "Синхронизация запущена. Ход виден в истории ниже." });
    }

    /// <summary>История проходов, свежие сверху.</summary>
    [HttpGet("runs")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<ActionResult<IEnumerable<OrgSyncRunResponse>>> Runs(
        [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var runs = await db.OrgSyncRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        var names = await NamesAsync(runs, ct);
        return Ok(runs.Select(r => ToResponse(r, names)));
    }

    private async Task<Dictionary<int, string>> NamesAsync(List<OrgSyncRun> runs, CancellationToken ct)
    {
        var ids = runs.Where(r => r.StartedByUserId is not null)
                      .Select(r => r.StartedByUserId!.Value)
                      .Distinct()
                      .ToList();

        if (ids.Count == 0) return [];

        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static OrgSyncRunResponse ToResponse(OrgSyncRun r, Dictionary<int, string> names)
    {
        var notes = string.IsNullOrEmpty(r.NotesJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(r.NotesJson) ?? [];

        return new OrgSyncRunResponse(
            r.Id, r.StartedAt, r.FinishedAt,
            r.Outcome.ToString(),
            r.StartedByUserId is int id && names.TryGetValue(id, out var name) ? name : null,
            r.UnitsReceived, r.UnitsCreated, r.UnitsUpdated, r.UnitsSkipped,
            r.EmployeesReceived, r.EmployeesMatched, r.EmployeesUnmatched, r.EmployeesUpdated,
            r.EmployeesDeactivated,
            r.Error, notes);
    }
}
