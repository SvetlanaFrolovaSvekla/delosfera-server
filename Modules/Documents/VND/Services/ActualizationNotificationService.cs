using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Раздел "Уведомления" → "Настройки рассылок" → "Нормотворчество" (см. ManagementPage/
/// NotificationMailingSettingsPage на фронте): ответственные сотрудники СП за актуализацию ВНД,
/// ежемесячная сводка им 1-го числа, критические напоминания по настраиваемым порогам и
/// единоразовая рассылка плана актуализации.
///
/// "Относится к СП" — тот же критерий, что колонки "Разработчик" и "Ответственные исполнители"
/// на странице "Планирование актуализации": документ учитывается для подразделения, если оно
/// либо разработчик документа, либо среди его ответственных исполнителей (см. FilterByOrgUnit).
/// Строится поверх VndService.SearchAsync с ignoreVisibilityRestriction: true — у фонового
/// воркера нет текущего HTTP-пользователя, и без этого флага сводка тихо теряла бы часть
/// документов (см. комментарий у SearchAsync).
/// </summary>
public class ActualizationNotificationService : IActualizationNotificationService
{
    // Те же статусы, что ACTUALIZATION_PLANNING_STATUSES на фронте
    // (src/utils/actualizationSearchRequest.ts) — действующие и уже проходящие/прошедшие
    // согласование хотя бы раз документы. Черновики и архив в сводку не попадают.
    private static readonly List<string> PlanningStatuses = ["active", "onact", "review", "consol"];

    private readonly DelosferaDbContext _db;
    private readonly IVndService _vndService;
    private readonly INotificationService _notifications;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ActualizationNotificationService> _logger;

    public ActualizationNotificationService(
        DelosferaDbContext db,
        IVndService vndService,
        INotificationService notifications,
        IFileStorageService fileStorage,
        ILogger<ActualizationNotificationService> logger)
    {
        _db = db;
        _vndService = vndService;
        _notifications = notifications;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<List<ActualizationNotificationResponsibleResponse>> GetResponsiblesAsync() =>
        await LoadResponsiblesAsync();

    public async Task<List<ActualizationNotificationResponsibleResponse>> SetResponsiblesAsync(
        SetActualizationNotificationResponsiblesRequest request)
    {
        var orgUnit = await _db.OrganizationUnits.FindAsync(request.OrgUnitId)
            ?? throw new KeyNotFoundException($"Подразделение с id={request.OrgUnitId} не найдено");

        var userIds = request.UserIds.Distinct().ToList();

        if (userIds.Count > 0)
        {
            // Ответственный не обязан числиться именно в этом СП — на экране назначения фильтр
            // поиска можно переключить на любое подразделение и добавить, например, куратора
            // или методолога, который ведёт актуализацию сразу нескольких СП. Проверяем здесь
            // только то, что все id вообще существуют.
            var existingIds = await _db.Users.Where(u => userIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();

            var missing = userIds.Except(existingIds).ToList();
            if (missing.Count > 0)
                throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");
        }

        var existing = await _db.Set<ActualizationNotificationResponsible>()
            .Where(x => x.OrgUnitId == request.OrgUnitId)
            .ToListAsync();

        var toRemove = existing.Where(x => !userIds.Contains(x.UserId)).ToList();
        var toAdd = userIds.Except(existing.Select(x => x.UserId)).ToList();

        _db.Set<ActualizationNotificationResponsible>().RemoveRange(toRemove);

        var now = DateTime.UtcNow;
        foreach (var userId in toAdd)
        {
            _db.Set<ActualizationNotificationResponsible>().Add(new ActualizationNotificationResponsible
            {
                OrgUnitId = request.OrgUnitId,
                UserId = userId,
                CreatedAt = now,
            });
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await _db.SaveChangesAsync();

        return await LoadResponsiblesAsync();
    }

    public async Task<ActualizationNotificationSettingsResponse> GetSettingsAsync()
    {
        var settings = await LoadSettingsEntityAsync();
        return ToSettingsResponse(settings);
    }

    public async Task<ActualizationNotificationSettingsResponse> UpdateSettingsAsync(
        UpdateActualizationNotificationSettingsRequest request)
    {
        if (request.CriticalReminderDays.Any(d => d < 0))
            throw new InvalidOperationException(
                "Критические напоминания задаются в днях до просрочки и не бывают отрицательными");

        var settings = await LoadSettingsEntityAsync();

        settings.MonthlyDigestEnabled = request.MonthlyDigestEnabled;
        settings.MonthlyDigestColumnsCsv = FormatColumns(request.MonthlyDigestColumns);
        settings.CriticalRemindersEnabled = request.CriticalRemindersEnabled;
        settings.CriticalReminderDaysCsv = FormatThresholdDays(request.CriticalReminderDays);
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ToSettingsResponse(settings);
    }

    public async Task<ActualizationNotificationPreviewResponse> PreviewMonthlyDigestAsync(
        int orgUnitId, string languageCode)
    {
        var orgUnit = await _db.OrganizationUnits.FindAsync(orgUnitId)
            ?? throw new KeyNotFoundException($"Подразделение с id={orgUnitId} не найдено");

        var recipients = await _db.Set<ActualizationNotificationResponsible>()
            .Include(x => x.User)
            .Where(x => x.OrgUnitId == orgUnitId)
            .OrderBy(x => x.User!.FullName)
            .Select(x => x.User!.FullName)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = FilterByOrgUnit(await LoadInvolvedRowsAsync(languageCode), orgUnitId);
        var counts = CountBuckets(rows);
        var (subject, body) = BuildDigestText(orgUnit.TitleRu, today, counts);

        return new ActualizationNotificationPreviewResponse
        {
            OrgUnitId = orgUnitId,
            OrgUnitName = orgUnit.TitleRu,
            Subject = subject,
            Body = body,
            AttachmentFileName = AttachmentFileName(orgUnit.TitleRu, today),
            RecipientNames = recipients,
            TotalCount = counts.Total,
            NormalCount = counts.Normal,
            ApproachingCount = counts.Approaching,
            CriticalCount = counts.Critical,
            OverdueCount = counts.Overdue,
        };
    }

    public async Task<int> SendMonthlyDigestAsync(DateOnly today, CancellationToken ct = default)
    {
        if (today.Day != 1) return 0;

        var settings = await LoadSettingsEntityAsync();
        if (!settings.MonthlyDigestEnabled) return 0;

        var columns = ParseColumns(settings.MonthlyDigestColumnsCsv);

        var allRows = await LoadInvolvedRowsAsync("ru");
        if (allRows.Count == 0) return 0;

        var responsibles = await _db.Set<ActualizationNotificationResponsible>()
            .Include(x => x.OrgUnit)
            .ToListAsync(ct);

        var sent = 0;

        foreach (var group in responsibles.GroupBy(x => x.OrgUnitId))
        {
            var orgUnitName = group.First().OrgUnit?.TitleRu ?? "—";
            var userIds = group.Select(x => x.UserId).Distinct().ToList();
            if (userIds.Count == 0) continue;

            var rows = FilterByOrgUnit(allRows, group.Key);

            // Нечего сообщать — письмо "0 документов" не несёт пользы, молча пропускаем это СП.
            if (rows.Count == 0) continue;

            var counts = CountBuckets(rows);
            var (subject, body) = BuildDigestText(orgUnitName, today, counts);
            var excelBytes = await _vndService.BuildActualizationPlanExcelAsync(rows, columns);

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = subject,
                BodyRu = body,
                Category = NotificationCategory.Vnd,
                Severity = counts.Overdue > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                EntityType = nameof(OrganizationUnit),
                EntityId = group.Key,
                Url = "/actualization/plan",
                UserIds = userIds,
                Attachment = new MailAttachment
                {
                    FileName = AttachmentFileName(orgUnitName, today),
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    Bytes = excelBytes,
                },
            }, null);

            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Ежемесячных сводок по актуализации ВНД разослано: {Count}", sent);

        return sent;
    }

    public async Task<int> SendCriticalRemindersAsync(DateOnly today, CancellationToken ct = default)
    {
        var settings = await LoadSettingsEntityAsync();
        if (!settings.CriticalRemindersEnabled) return 0;

        var thresholds = ParseThresholdDays(settings.CriticalReminderDaysCsv);
        if (thresholds.Count == 0) return 0;

        var allRows = await LoadInvolvedRowsAsync("ru");

        // Только документы, у которых сегодня число дней до срока актуализации совпадает
        // ровно с одним из настроенных порогов — "до наступления просрочки", просроченные
        // (отрицательный остаток) сюда не попадают: это уже случившийся факт, а не напоминание.
        var due = allRows
            .Where(r => r.DueActualizationDate.HasValue)
            .Select(r => (Row: r, DaysLeft: r.DueActualizationDate!.Value.DayNumber - today.DayNumber))
            .Where(x => x.DaysLeft >= 0 && thresholds.Contains(x.DaysLeft))
            .ToList();

        if (due.Count == 0) return 0;

        // СП, которым сегодня есть о чём напомнить — либо как разработчику, либо как
        // ответственному исполнителю документа (тот же критерий, что и у месячной сводки).
        var byOrgUnit = new Dictionary<int, List<(VndResponse Row, int DaysLeft)>>();
        foreach (var (row, daysLeft) in due)
        {
            var orgUnitIds = new List<int> {row.DeveloperId};
            orgUnitIds.AddRange(row.ResponsibleExecutorIds);

            foreach (var orgUnitId in orgUnitIds.Distinct())
            {
                if (!byOrgUnit.TryGetValue(orgUnitId, out var list))
                {
                    list = [];
                    byOrgUnit[orgUnitId] = list;
                }
                list.Add((row, daysLeft));
            }
        }

        var relevantOrgUnitIds = byOrgUnit.Keys.ToList();

        var orgUnits = await _db.OrganizationUnits
            .Where(o => relevantOrgUnitIds.Contains(o.Id))
            .Select(o => new {o.Id, o.TitleRu, o.CuratorUserId})
            .ToListAsync(ct);

        var responsibleUserIdsByOrgUnit = await _db.Set<ActualizationNotificationResponsible>()
            .Where(x => relevantOrgUnitIds.Contains(x.OrgUnitId))
            .Select(x => new {x.OrgUnitId, x.UserId})
            .ToListAsync(ct);

        var sent = 0;

        foreach (var (orgUnitId, rows) in byOrgUnit)
        {
            var orgUnit = orgUnits.FirstOrDefault(o => o.Id == orgUnitId);
            var orgUnitName = orgUnit?.TitleRu ?? "—";

            var userIds = responsibleUserIdsByOrgUnit
                .Where(x => x.OrgUnitId == orgUnitId)
                .Select(x => x.UserId)
                .ToList();

            // Куратор СП получает то же напоминание, что и ответственные сотрудники — он не
            // обязательно входит в их состав (см. OrganizationUnit.CuratorUserId).
            if (orgUnit?.CuratorUserId is { } curatorId)
                userIds.Add(curatorId);

            userIds = userIds.Distinct().ToList();

            // Ответственных и куратора для этого СП ещё не назначили — уведомлять некого,
            // молча пропускаем (та же логика, что у "нечего сообщать" в месячной сводке).
            if (userIds.Count == 0) continue;

            var (subject, body) = BuildCriticalReminderText(orgUnitName, today, rows);

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = subject,
                BodyRu = body,
                Category = NotificationCategory.Vnd,
                Severity = rows.Any(x => x.DaysLeft == 0) ? NotificationSeverity.Urgent : NotificationSeverity.Warning,
                EntityType = nameof(OrganizationUnit),
                EntityId = orgUnitId,
                Url = "/actualization/plan",
                UserIds = userIds,
            }, null);

            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Критических напоминаний по актуализации ВНД разослано: {Count}", sent);

        return sent;
    }

    public async Task<SendActualizationOneTimeMailingResponse> SendOneTimeMailingAsync(
        SendActualizationOneTimeMailingRequest request, int? currentUserId, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Укажите тему письма");

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new InvalidOperationException("Укажите текст сообщения");

        if (request.IncludePlan && request.PlanExport is null)
            throw new InvalidOperationException(
                "Включён план актуализации, но не заданы его настройки (фильтр и колонки)");

        var orgUnitIds = request.ResponsibleOrgUnitIds.Distinct().ToList();

        var fromResponsibles = new List<int>();
        if (orgUnitIds.Count > 0)
        {
            fromResponsibles = await _db.Set<ActualizationNotificationResponsible>()
                .Where(x => orgUnitIds.Contains(x.OrgUnitId))
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }

        var recipientIds = fromResponsibles.Concat(request.UserIds).Distinct().ToList();

        if (recipientIds.Count == 0)
            throw new InvalidOperationException(
                "Получателей не найдено — выберите СП с назначенными ответственными сотрудниками либо отдельных пользователей");

        var recipients = await _db.Users
            .Where(u => recipientIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var missing = recipientIds.Except(recipients.Select(u => u.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");

        // Единоразовая рассылка — только системное уведомление внутри Делосферы, без почты
        // (SkipEmail: true) и без кнопки "Перейти к задаче" (Url: null) — так требуется явно,
        // в отличие от ежемесячной сводки и критических напоминаний выше. План, если включён,
        // сохраняется как обычный файл системы (см. IFileStorageService) и виден получателю
        // прямо в карточке уведомления (см. Notification.AttachmentFileId), а не как вложение
        // письма — почта тут вообще не участвует.
        int? attachmentFileId = null;
        if (request.IncludePlan)
        {
            var ownerId = currentUserId
                ?? throw new InvalidOperationException("Не удалось определить пользователя для сохранения файла плана");

            var excelBytes = await _vndService.ExportActualizationPlanAsync(request.PlanExport!, languageCode);
            var fileName = $"План актуализации ({DateTime.UtcNow:dd.MM.yyyy}).xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileAttachment = await _fileStorage.SaveGeneratedAsync(excelBytes, fileName, contentType, ownerId);
            attachmentFileId = fileAttachment.Id;
        }

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = request.Subject.Trim(),
            BodyRu = request.Message.Trim(),
            Category = NotificationCategory.Vnd,
            Severity = NotificationSeverity.Info,
            Url = null,
            UserIds = recipientIds,
            AttachmentFileId = attachmentFileId,
            SkipEmail = true,
        }, currentUserId);

        return new SendActualizationOneTimeMailingResponse
        {
            RecipientCount = recipients.Count,
            RecipientNames = recipients.Select(u => u.FullName).ToList(),
        };
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<List<ActualizationNotificationResponsibleResponse>> LoadResponsiblesAsync()
    {
        var entities = await _db.Set<ActualizationNotificationResponsible>()
            .Include(x => x.OrgUnit)
            .Include(x => x.User)
            .ThenInclude(u => u!.OrgUnit)
            .ToListAsync();

        return entities
            .OrderBy(x => x.OrgUnit?.TitleRu)
            .ThenBy(x => x.User?.FullName)
            .Select(x => new ActualizationNotificationResponsibleResponse
            {
                Id = x.Id,
                OrgUnitId = x.OrgUnitId,
                OrgUnitName = x.OrgUnit?.TitleRu ?? "—",
                UserId = x.UserId,
                UserFullName = x.User?.FullName ?? "—",
                // Своё СП сотрудника — не обязательно то же, за что он отвечает (см. комментарий
                // у SetResponsiblesAsync): нужно фронту, чтобы отличить в списке назначения
                // "свой"/"чужой" сотрудник.
                UserOrgUnitId = x.User?.OrgUnitId,
                UserOrgUnitName = x.User?.OrgUnit?.TitleRu,
            })
            .ToList();
    }

    private async Task<ActualizationNotificationSettings> LoadSettingsEntityAsync()
    {
        var settings = await _db.Set<ActualizationNotificationSettings>().FirstOrDefaultAsync();
        if (settings is not null) return settings;

        // Значения по умолчанию засеяны миграцией; страховка на случай пустой таблицы —
        // тот же приём, что ActualizationBucketSettingsService.LoadAsync.
        settings = new ActualizationNotificationSettings();
        _db.Set<ActualizationNotificationSettings>().Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private static ActualizationNotificationSettingsResponse ToSettingsResponse(ActualizationNotificationSettings s) => new()
    {
        MonthlyDigestEnabled = s.MonthlyDigestEnabled,
        MonthlyDigestColumns = ParseColumns(s.MonthlyDigestColumnsCsv),
        CriticalRemindersEnabled = s.CriticalRemindersEnabled,
        CriticalReminderDays = ParseThresholdDays(s.CriticalReminderDaysCsv),
    };

    private static List<string> ParseColumns(string csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string FormatColumns(List<string> columns) => string.Join(",", columns.Distinct());

    private static List<int> ParseThresholdDays(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var value) ? value : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToList();
    }

    private static string FormatThresholdDays(List<int> days) =>
        string.Join(",", days.Where(d => d >= 0).Distinct().OrderBy(d => d));

    private async Task<List<VndResponse>> LoadInvolvedRowsAsync(string languageCode)
    {
        var filter = new VndSearchRequest {Statuses = PlanningStatuses};

        // ignoreVisibilityRestriction: true — см. комментарий у VndService.SearchAsync и у
        // IActualizationNotificationService выше.
        return await _vndService.SearchAsync(filter, languageCode, ignoreVisibilityRestriction: true);
    }

    /// <summary>СП "относится" к документу, если оно разработчик либо один из ответственных
    /// исполнителей — та же логика, что читают колонки "Разработчик"/"Ответственные
    /// исполнители" на странице "Планирование актуализации".</summary>
    private static List<VndResponse> FilterByOrgUnit(List<VndResponse> rows, int orgUnitId) =>
        rows.Where(r => r.DeveloperId == orgUnitId || r.ResponsibleExecutorIds.Contains(orgUnitId)).ToList();

    private static BucketCounts CountBuckets(List<VndResponse> rows) => new(
        rows.Count,
        rows.Count(r => r.ActualizationBucket == "normal"),
        rows.Count(r => r.ActualizationBucket == "approaching"),
        rows.Count(r => r.ActualizationBucket == "critical"),
        rows.Count(r => r.ActualizationBucket == "overdue"));

    private static (string Subject, string Body) BuildDigestText(
        string orgUnitName, DateOnly today, BucketCounts counts)
    {
        var subject = $"План актуализации ВНД: сводка на {today:MM.yyyy} — {orgUnitName}";

        var body = $"""
            Ежемесячная сводка по плану актуализации ВНД для подразделения «{orgUnitName}» на {today:dd.MM.yyyy}.

            Всего ВНД, относящихся к подразделению (по полю «Разработчик» либо «Ответственные исполнители»): {counts.Total}.

            В норме: {counts.Normal}
            Приближается срок: {counts.Approaching}
            Критичный срок: {counts.Critical}
            Просрочено: {counts.Overdue}

            Полный список — во вложенном файле Excel.
            """;

        return (subject, body);
    }

    private static (string Subject, string Body) BuildCriticalReminderText(
        string orgUnitName, DateOnly today, List<(VndResponse Row, int DaysLeft)> rows)
    {
        var subject = $"Критическое напоминание об актуализации ВНД — {orgUnitName}";

        var lines = rows
            .OrderBy(x => x.DaysLeft)
            .Select(x => $"«{x.Row.Name}» ({x.Row.Code}) — {DaysLeftLabel(x.DaysLeft)}")
            .ToList();

        var body = $"""
            Напоминание о приближающемся сроке актуализации ВНД для подразделения «{orgUnitName}» на {today:dd.MM.yyyy}.

            {string.Join("\n", lines)}

            Перейдите в раздел «Планирование актуализации», чтобы начать актуализацию.
            """;

        return (subject, body);
    }

    private static string DaysLeftLabel(int daysLeft) => daysLeft switch
    {
        0 => "срок сегодня",
        1 => "срок завтра",
        _ => $"срок через {daysLeft} дн.",
    };

    private static string AttachmentFileName(string orgUnitName, DateOnly today) =>
        $"План актуализации — {SanitizeFileNamePart(orgUnitName)} ({today:MM.yyyy}).xlsx";

    /// <summary>Имя файла вложения не должно ломаться в почтовом клиенте получателя —
    /// вырезаем символы, недопустимые в имени файла Windows.</summary>
    private static string SanitizeFileNamePart(string name) =>
        new string(name.Where(c => !"\\/:*?\"<>|".Contains(c)).ToArray()).Trim();

    private readonly record struct BucketCounts(int Total, int Normal, int Approaching, int Critical, int Overdue);
}
