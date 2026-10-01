using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndNewRedactionMailingService
{
    Task<VndNewRedactionMailingResponse> GetAsync(CancellationToken ct = default);
    Task<VndNewRedactionMailingResponse> UpdateAsync(UpdateVndNewRedactionMailingRequest request, CancellationToken ct = default);

    /// <summary>
    /// Разослать уведомление о том, что у ВНД стала действующей новая редакция (консолидация)
    /// или появилась первая редакция нового ВНД. Вызывается ПОСЛЕ сохранения перехода в статус
    /// «Действующий». Ошибки рассылки не пробрасываются — консолидация от почты зависеть не должна.
    /// </summary>
    /// <param name="hadChanges">false — консолидация актуализации «без изменений»: текст
    /// редакции прежний, письмо сообщает о подтверждении актуальности. null/true — новая редакция.</param>
    Task NotifyNewRedactionAsync(int vndId, bool? hadChanges = null, CancellationToken ct = default);
}

/// <summary>
/// Рассылка «Новые редакции ВНД» (см. <see cref="VndNewRedactionMailingSettings"/>):
/// настройки, состав получателей и отправка письма при каждой консолидации.
/// </summary>
public class VndNewRedactionMailingService : IVndNewRedactionMailingService
{
    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IMailQueue _mail;
    private readonly ILogger<VndNewRedactionMailingService> _logger;

    public VndNewRedactionMailingService(
        DelosferaDbContext db,
        INotificationService notifications,
        IMailQueue mail,
        ILogger<VndNewRedactionMailingService> logger)
    {
        _db = db;
        _notifications = notifications;
        _mail = mail;
        _logger = logger;
    }

    public async Task<VndNewRedactionMailingResponse> GetAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);
        return await BuildResponseAsync(settings, ct);
    }

    public async Task<VndNewRedactionMailingResponse> UpdateAsync(
        UpdateVndNewRedactionMailingRequest request, CancellationToken ct = default)
    {
        // Включённая рассылка без канала доставки никому ничего не сообщит — почти наверняка
        // забыли отметить канал (та же проверка, что у ежемесячной сводки).
        if (request.Enabled && !request.NotifyInApp && !request.NotifyEmail)
            throw new InvalidOperationException(
                "Выберите хотя бы один канал доставки: письмо на почту или уведомление в системе");

        var userIds = request.UserIds.Distinct().ToList();

        if (request.Enabled && userIds.Count == 0)
            throw new InvalidOperationException("Добавьте хотя бы одного получателя рассылки");

        if (userIds.Count > 0)
        {
            var existingIds = await _db.Users.Where(u => userIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
            var missing = userIds.Except(existingIds).ToList();
            if (missing.Count > 0)
                throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");
        }

        var settings = await LoadSettingsAsync(ct);
        settings.Enabled = request.Enabled;
        settings.NotifyInApp = request.NotifyInApp;
        settings.NotifyEmail = request.NotifyEmail;

        var recipients = _db.Set<VndNewRedactionMailingRecipient>();
        var current = await recipients.ToListAsync(ct);

        recipients.RemoveRange(current.Where(r => !userIds.Contains(r.UserId)));

        var now = DateTime.UtcNow;
        foreach (var userId in userIds.Except(current.Select(r => r.UserId)))
            recipients.Add(new VndNewRedactionMailingRecipient {UserId = userId, CreatedAt = now});

        await _db.SaveChangesAsync(ct);
        return await BuildResponseAsync(settings, ct);
    }

    public async Task NotifyNewRedactionAsync(int vndId, bool? hadChanges = null, CancellationToken ct = default)
    {
        try
        {
            var settings = await _db.Set<VndNewRedactionMailingSettings>().AsNoTracking().FirstOrDefaultAsync(ct);
            if (settings is null || !settings.Enabled) return;
            if (!settings.NotifyInApp && !settings.NotifyEmail) return;

            var userIds = await _db.Set<VndNewRedactionMailingRecipient>()
                .Select(r => r.UserId)
                .ToListAsync(ct);
            if (userIds.Count == 0) return;

            var vnd = await _db.VndDocuments
                .AsNoTracking()
                .Where(v => v.Id == vndId)
                .Select(v => new
                {
                    v.Code,
                    v.TitleRu,
                    v.AdoptionCode,
                    v.AdoptionDate,
                    v.EffectiveDate,
                    RedactionNumber = v.CurrentRedaction != null ? (int?)v.CurrentRedaction.Number : null,
                    TypeName = v.Type != null ? v.Type.TitleRu : null,
                    DeveloperName = v.Developer != null ? v.Developer.TitleRu : null,
                })
                .FirstOrDefaultAsync(ct);
            if (vnd is null) return;

            var isFirst = vnd.RedactionNumber is null or <= 1;
            var noChanges = hadChanges == false && !isFirst;
            var redactionLabel = vnd.RedactionNumber.HasValue ? $"Р{vnd.RedactionNumber}" : "первая редакция";

            var subject = isFirst
                ? $"Новый ВНД: «{vnd.TitleRu}» ({vnd.Code})"
                : noChanges
                    ? $"ВНД актуализирован без изменений: «{vnd.TitleRu}» ({vnd.Code})"
                    : $"ВНД обновлён: «{vnd.TitleRu}» ({vnd.Code}) — редакция {redactionLabel}";

            var lines = new List<string>
            {
                isFirst
                    ? $"Появился новый внутренний нормативный документ — «{vnd.TitleRu}», действует его первая редакция."
                    : noChanges
                        ? $"Внутренний нормативный документ «{vnd.TitleRu}» актуализирован без изменений: действующая редакция {redactionLabel} подтверждена."
                        : $"Внутренний нормативный документ «{vnd.TitleRu}» обновлён: действует новая редакция {redactionLabel}.",
                "",
                $"Код: {vnd.Code}",
            };
            if (!string.IsNullOrWhiteSpace(vnd.TypeName)) lines.Add($"Вид документа: {vnd.TypeName}");
            if (!string.IsNullOrWhiteSpace(vnd.DeveloperName)) lines.Add($"Разработчик: {vnd.DeveloperName}");
            if (!string.IsNullOrWhiteSpace(vnd.AdoptionCode) || vnd.AdoptionDate.HasValue)
                lines.Add($"Утверждён: № {vnd.AdoptionCode ?? "—"}{(vnd.AdoptionDate.HasValue ? $" от {vnd.AdoptionDate.Value:dd.MM.yyyy}" : "")}");
            if (vnd.EffectiveDate.HasValue)
                lines.Add($"Вступает в силу: {vnd.EffectiveDate.Value:dd.MM.yyyy}");
            lines.Add("");
            lines.Add("Документ доступен в Делосфере по ссылке ниже.");

            var body = string.Join("\n", lines);
            var url = $"/base-vnd/{vndId}";

            if (settings.NotifyInApp)
            {
                // Уведомление в системе; почтовая копия — по настройке (тот же приём, что у
                // ежемесячной сводки по актуализации).
                await _notifications.CreateAsync(new CreateNotificationRequest
                {
                    TitleRu = subject,
                    BodyRu = body,
                    Category = NotificationCategory.Vnd,
                    Severity = NotificationSeverity.Info,
                    EntityType = nameof(VndDocument),
                    EntityId = vndId,
                    Url = url,
                    UserIds = userIds,
                    SkipEmail = !settings.NotifyEmail,
                }, null);
            }
            else
            {
                // Только почта — письмо ставим в очередь напрямую.
                await _mail.EnqueueAsync(userIds, subject, body, url, null, null, ct);
            }

            _logger.LogInformation(
                "Рассылка о новой редакции ВНД {VndId} ({Code}): получателей {Count}", vndId, vnd.Code, userIds.Count);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Консолидация уже сохранена; сбой рассылки не должен превращать её в ошибку для
            // пользователя — только в запись в журнале.
            _logger.LogError(e, "Не удалось разослать уведомление о новой редакции ВНД {VndId}", vndId);
        }
    }

    private async Task<VndNewRedactionMailingSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.Set<VndNewRedactionMailingSettings>().FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        // Запись одна на систему и создаётся при первом обращении — с выключенной рассылкой.
        settings = new VndNewRedactionMailingSettings {Enabled = false, NotifyInApp = false, NotifyEmail = true};
        _db.Set<VndNewRedactionMailingSettings>().Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task<VndNewRedactionMailingResponse> BuildResponseAsync(
        VndNewRedactionMailingSettings settings, CancellationToken ct)
    {
        var recipients = await _db.Set<VndNewRedactionMailingRecipient>()
            .AsNoTracking()
            .OrderBy(r => r.User!.FullName)
            .Select(r => new VndNewRedactionMailingRecipientResponse
            {
                UserId = r.UserId,
                FullName = r.User!.FullName,
                Email = r.User.Email,
                OrgUnitName = r.User.OrgUnit != null ? r.User.OrgUnit.TitleRu : null,
                PositionName = r.User.Position != null ? r.User.Position.TitleRu : null,
                IsInactive = !r.User.IsActive || r.User.BlockedAt != null,
            })
            .ToListAsync(ct);

        return new VndNewRedactionMailingResponse
        {
            Enabled = settings.Enabled,
            NotifyInApp = settings.NotifyInApp,
            NotifyEmail = settings.NotifyEmail,
            Recipients = recipients,
        };
    }
}
