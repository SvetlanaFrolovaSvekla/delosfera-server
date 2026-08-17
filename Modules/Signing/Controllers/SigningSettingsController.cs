using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Signing.Controllers;

public class SigningSettingsRequest
{
    public bool TimestampEnabled { get; set; }
    public string? TimestampAuthorityUrl { get; set; }
    public bool TimestampRequired { get; set; }
    public int TimestampTimeoutSeconds { get; set; } = 15;

    public bool RevocationCheckEnabled { get; set; }
    public bool RevocationStrict { get; set; }
    public int RevocationRecheckHours { get; set; } = 24;
}

/// <summary>
/// Настройки квалифицированной подписи: служба меток времени и проверка отзыва (Б-18).
///
/// Обе настройки меняют юридический вес подписи, поэтому каждая правка идёт в журнал:
/// «почему у подписи от прошлого месяца нет метки» — вопрос, на который должен быть
/// ответ, а не догадка.
/// </summary>
[ApiController]
[Authorize]
[Route("api/signing/settings")]
[Tags("Подписание")]
public class SigningSettingsController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public SigningSettingsController(
        DelosferaDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await _db.SigningSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        return Ok(new
        {
            timestampEnabled = settings?.TimestampEnabled ?? false,
            timestampAuthorityUrl = settings?.TimestampAuthorityUrl,
            timestampRequired = settings?.TimestampRequired ?? false,
            timestampTimeoutSeconds = settings?.TimestampTimeoutSeconds ?? 15,

            revocationCheckEnabled = settings?.RevocationCheckEnabled ?? false,
            revocationStrict = settings?.RevocationStrict ?? false,
            revocationRecheckHours = settings?.RevocationRecheckHours ?? 24,

            updatedAt = settings?.UpdatedAt,

            // Сколько подписей уже стоит без метки: включение метки не задним числом,
            // и эти подписи останутся без неё навсегда.
            signaturesWithoutTimestamp = await _db.Signatures
                .CountAsync(s => s.Level == SignatureLevel.Qualified && s.TimestampedAt == null, ct),
        });
    }

    [HttpPut]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update([FromBody] SigningSettingsRequest request, CancellationToken ct)
    {
        if (request.TimestampEnabled && string.IsNullOrWhiteSpace(request.TimestampAuthorityUrl))
            return BadRequest(new {message = "Укажите адрес службы меток времени"});

        if (!string.IsNullOrWhiteSpace(request.TimestampAuthorityUrl))
        {
            if (!Uri.TryCreate(request.TimestampAuthorityUrl.Trim(), UriKind.Absolute, out var url))
                return BadRequest(new {message = "Адрес службы меток времени разобран неверно"});

            if (url.Scheme is not ("http" or "https"))
                return BadRequest(new {message = "Служба меток времени вызывается по http или https"});
        }

        // Обязательность метки без самой метки означала бы, что подписать нельзя ничего.
        if (request.TimestampRequired && !request.TimestampEnabled)
            return BadRequest(new
            {
                message = "Метка не может быть обязательной, пока сама метка выключена",
            });

        if (request.RevocationStrict && !request.RevocationCheckEnabled)
            return BadRequest(new
            {
                message = "Строгий режим отзыва не имеет смысла, пока проверка отзыва выключена",
            });

        var settings = await _db.SigningSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new SigningSettings();
            _db.SigningSettings.Add(settings);
        }

        var было = new
        {
            settings.TimestampEnabled,
            settings.TimestampRequired,
            settings.RevocationCheckEnabled,
            settings.RevocationStrict,
        };

        settings.TimestampEnabled = request.TimestampEnabled;
        settings.TimestampAuthorityUrl = string.IsNullOrWhiteSpace(request.TimestampAuthorityUrl)
            ? null
            : request.TimestampAuthorityUrl.Trim();
        settings.TimestampRequired = request.TimestampRequired;
        settings.TimestampTimeoutSeconds = Math.Clamp(request.TimestampTimeoutSeconds, 3, 120);

        settings.RevocationCheckEnabled = request.RevocationCheckEnabled;
        settings.RevocationStrict = request.RevocationStrict;
        settings.RevocationRecheckHours = Math.Clamp(request.RevocationRecheckHours, 1, 720);

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("SigningSettings", settings.Id, "Updated", _currentUser.UserId, new
        {
            было,
            стало = new
            {
                settings.TimestampEnabled,
                settings.TimestampRequired,
                settings.RevocationCheckEnabled,
                settings.RevocationStrict,
            },
            settings.TimestampAuthorityUrl,
        });

        return Ok(new {settings.Id, settings.UpdatedAt});
    }

    /// <summary>
    /// Проверить связь со службой меток, не подписывая ничего. Иначе первым, на кого
    /// упадёт неверный адрес, окажется подписант приказа.
    /// </summary>
    [HttpPost("timestamp/test")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> TestTimestamp(
        [FromServices] ITimestampService timestamps, CancellationToken ct)
    {
        var settings = await _db.SigningSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings is null || !settings.TimestampEnabled)
            return BadRequest(new {message = "Метка времени выключена — включите и сохраните настройки"});

        // Штампуем произвольные байты: службе всё равно, что за свёртка ей пришла,
        // а трогать чью-то настоящую подпись ради проверки связи незачем.
        var result = await timestamps.StampAsync(
            System.Text.Encoding.UTF8.GetBytes($"delosfera-timestamp-test-{settings.Id}"), ct);

        return result.Obtained
            ? Ok(new {ok = true, at = result.At, authority = result.Authority})
            : BadRequest(new {message = result.Reason ?? "Метку получить не удалось"});
    }
}
