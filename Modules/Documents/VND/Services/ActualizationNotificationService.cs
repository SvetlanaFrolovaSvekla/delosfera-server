using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.Models;
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
public static class ActualizationNotificationMessages
{
    /// <summary>Общий для всех подразделений стабильный префикс темы ежемесячной сводки (дальше
    /// идут месяц/год и название подразделения — см. BuildDigestText) — вынесен константой, чтобы
    /// ActualizationNotificationWorker мог по нему узнать в Notifications/OutgoingEmail,
    /// отправляли ли сводку сегодня уже (см. комментарий на
    /// ActualizationNotificationWorker.GetLastMonthlyDigestRunOnFromHistoryAsync), не дублируя
    /// строку.</summary>
    public const string MonthlyDigestSubjectPrefix = "План актуализации ВНД: сводка на ";
}

public class ActualizationNotificationService : IActualizationNotificationService
{
    // Те же статусы, что ACTUALIZATION_PLANNING_STATUSES на фронте
    // (src/utils/actualizationSearchRequest.ts) — действующие и уже проходящие/прошедшие
    // согласование хотя бы раз документы. Черновики и архив в сводку не попадают.
    private static readonly List<string> PlanningStatuses = ["active", "onact", "review", "consol"];

    private readonly DelosferaDbContext _db;
    private readonly IVndService _vndService;
    private readonly INotificationService _notifications;
    private readonly IMailQueue _mail;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ActualizationNotificationService> _logger;

    public ActualizationNotificationService(
        DelosferaDbContext db,
        IVndService vndService,
        INotificationService notifications,
        IMailQueue mail,
        IFileStorageService fileStorage,
        ILogger<ActualizationNotificationService> logger)
    {
        _db = db;
        _vndService = vndService;
        _notifications = notifications;
        _mail = mail;
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

        // Включённая рассылка без единого канала доставки ничего не сообщает никому — это
        // почти наверняка ошибка администратора (забыл отметить канал), а не осознанное решение
        // "рассылать в никуда". Проверяем здесь, а не только в SendMonthlyDigestAsync/
        // SendCriticalRemindersAsync — иначе сохранить такую комбинацию можно, а сама рассылка
        // потом молча ничего не отправит, не дав об этом знать.
        if (request.MonthlyDigestEnabled && !request.MonthlyDigestNotifyInApp && !request.MonthlyDigestNotifyEmail)
            throw new InvalidOperationException(
                "Для ежемесячной сводки выберите хотя бы один канал — в системе или по почте");

        if (request.CriticalRemindersEnabled && !request.CriticalRemindersNotifyInApp && !request.CriticalRemindersNotifyEmail)
            throw new InvalidOperationException(
                "Для критических напоминаний выберите хотя бы один канал — в системе или по почте");

        var settings = await LoadSettingsEntityAsync();

        settings.MonthlyDigestEnabled = request.MonthlyDigestEnabled;
        settings.MonthlyDigestColumnsCsv = FormatColumns(request.MonthlyDigestColumns);
        settings.MonthlyDigestNotifyInApp = request.MonthlyDigestNotifyInApp;
        settings.MonthlyDigestNotifyEmail = request.MonthlyDigestNotifyEmail;
        settings.CriticalRemindersEnabled = request.CriticalRemindersEnabled;
        settings.CriticalReminderDaysCsv = FormatThresholdDays(request.CriticalReminderDays);
        settings.CriticalRemindersNotifyInApp = request.CriticalRemindersNotifyInApp;
        settings.CriticalRemindersNotifyEmail = request.CriticalRemindersNotifyEmail;
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

            // Оба канала выключены не должно долетать сюда — UpdateSettingsAsync не даёт
            // сохранить такую комбинацию, пока MonthlyDigestEnabled = true. Проверяем всё равно:
            // это дешевле, чем разбираться, почему сводка "ушла в никуда", если инвариант
            // где-то всё же нарушится (например, старые данные без миграции значений по
            // умолчанию).
            if (!settings.MonthlyDigestNotifyInApp && !settings.MonthlyDigestNotifyEmail) continue;

            var counts = CountBuckets(rows);
            var (subject, body) = BuildDigestText(orgUnitName, today, counts);
            var excelBytes = await _vndService.BuildActualizationPlanExcelAsync(rows, columns);
            var attachment = new MailAttachment
            {
                FileName = AttachmentFileName(orgUnitName, today),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Bytes = excelBytes,
            };

            if (settings.MonthlyDigestNotifyInApp)
            {
                // Системное уведомление, с почтовой копией по желанию (SkipEmail) — тот же
                // приём, что и раньше, только SkipEmail теперь управляется настройкой, а не
                // всегда false.
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
                    Attachment = attachment,
                    SkipEmail = !settings.MonthlyDigestNotifyEmail,
                }, null);
            }
            else
            {
                // Только почта, без записи в системе — уведомление создавать не для чего, письмо
                // ставим в очередь напрямую (тот же путь, каким CreateAsync пользуется внутри).
                await _mail.EnqueueAsync(userIds, subject, body, "/actualization/plan", null, attachment);
            }

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

        // Документы, у которых срок актуализации сегодня попадает в один из настроенных
        // порогов (или уже прошёл его, но ещё не наступила просрочка — отрицательный остаток
        // сюда не попадает, это уже случившийся факт, а не напоминание).
        //
        // Раньше здесь стояло точное совпадение (DaysLeft == порог): если ровно в нужный день
        // воркер не отработал (простой/деплой), напоминание для этого порога терялось навсегда —
        // на следующий день DaysLeft уже меньше порога и условие никогда больше не срабатывает.
        // Теперь берём "порог достигнут или пройден" (DaysLeft <= порог) и дополнительно
        // проверяем по ActivityLogEntry (см. ниже), что именно для ЭТОГО порога и ЭТОГО срока
        // актуализации напоминание ещё не отправлялось — иначе оно уходило бы каждый день,
        // пока остаток не станет отрицательным.
        var candidates = allRows
            .Where(r => r.DueActualizationDate.HasValue)
            .Select(r => (Row: r, DaysLeft: r.DueActualizationDate!.Value.DayNumber - today.DayNumber))
            .Where(x => x.DaysLeft >= 0 && thresholds.Any(t => x.DaysLeft <= t))
            .ToList();

        if (candidates.Count == 0) return 0;

        var candidateIds = candidates.Select(x => x.Row.Id).Distinct().ToList();
        var alreadySent = await LoadSentReminderMarkersAsync(candidateIds, ct);

        var due = new List<(VndResponse Row, int DaysLeft)>();
        var newlyCoveredThresholds = new Dictionary<int, List<int>>();

        foreach (var (row, daysLeft) in candidates)
        {
            var crossedThresholds = thresholds.Where(t => daysLeft <= t).ToList();
            var sentForThisDueDate = alreadySent.TryGetValue(row.Id, out var sentMarkers)
                ? sentMarkers.Where(x => x.DueDate == row.DueActualizationDate!.Value).Select(x => x.Threshold).ToHashSet()
                : [];

            var newThresholds = crossedThresholds.Where(t => !sentForThisDueDate.Contains(t)).ToList();
            if (newThresholds.Count == 0) continue; // все пороги для этого срока уже отправлялись

            due.Add((row, daysLeft));
            newlyCoveredThresholds[row.Id] = newThresholds;
        }

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

            // Та же защита, что у ежемесячной сводки выше — см. её комментарий.
            if (!settings.CriticalRemindersNotifyInApp && !settings.CriticalRemindersNotifyEmail) continue;

            var (subject, body) = BuildCriticalReminderText(orgUnitName, today, rows);
            var severity = rows.Any(x => x.DaysLeft == 0) ? NotificationSeverity.Urgent : NotificationSeverity.Warning;

            if (settings.CriticalRemindersNotifyInApp)
            {
                await _notifications.CreateAsync(new CreateNotificationRequest
                {
                    TitleRu = subject,
                    BodyRu = body,
                    Category = NotificationCategory.Vnd,
                    Severity = severity,
                    EntityType = nameof(OrganizationUnit),
                    EntityId = orgUnitId,
                    Url = "/actualization/plan",
                    UserIds = userIds,
                    SkipEmail = !settings.CriticalRemindersNotifyEmail,
                }, null);
            }
            else
            {
                await _mail.EnqueueAsync(userIds, subject, body, "/actualization/plan", null, null);
            }

            sent++;
        }

        // --- Отмечаем, для каких порогов по каждому документу напоминание отправлено в рамках
        // ЭТОГО срока актуализации (DueActualizationDate) — см. комментарий выше и
        // LoadSentReminderMarkersAsync. Отмечаем оптимистично, после рассылки по всем СП: если
        // документ относится к нескольким СП, порог помечается один раз, а не по разу на каждое.
        foreach (var (vndId, thresholdsCovered) in newlyCoveredThresholds)
        {
            var row = due.First(x => x.Row.Id == vndId).Row;
            foreach (var threshold in thresholdsCovered)
            {
                _db.Set<ActivityLogEntry>().Add(new ActivityLogEntry
                {
                    Module = ActivityModules.Vnd,
                    EntityId = vndId,
                    EntityCode = row.Code,
                    Kind = ActivityEventKind.ActualizationReminderSent,
                    ActorUserId = null,
                    // Служебный формат, не для показа пользователю (см. ActivityEventKind.
                    // ActualizationReminderSent) — разбирается в LoadSentReminderMarkersAsync.
                    TextRu = $"threshold={threshold};due={row.DueActualizationDate:yyyy-MM-dd}",
                    Url = "/base-vnd/" + vndId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        if (sent > 0)
            _logger.LogInformation("Критических напоминаний по актуализации ВНД разослано: {Count}", sent);

        return sent;
    }

    /// <summary>Пороги критических напоминаний, уже отправленные по каждому документу, вместе со
    /// сроком актуализации, для которого они отправлялись — см. комментарий в
    /// SendCriticalRemindersAsync. Срок нужен, чтобы новый цикл актуализации (с новым
    /// DueActualizationDate) не считался "уже уведомлённым" по меткам прошлого цикла.</summary>
    private async Task<Dictionary<int, List<(int Threshold, DateOnly DueDate)>>> LoadSentReminderMarkersAsync(
        List<int> vndIds, CancellationToken ct)
    {
        var markers = await _db.Set<ActivityLogEntry>()
            .Where(x => x.Module == ActivityModules.Vnd
                        && x.Kind == ActivityEventKind.ActualizationReminderSent
                        && vndIds.Contains(x.EntityId))
            .Select(x => new {x.EntityId, x.TextRu})
            .ToListAsync(ct);

        var result = new Dictionary<int, List<(int, DateOnly)>>();

        foreach (var marker in markers)
        {
            // Формат "threshold=N;due=yyyy-MM-dd", см. запись в SendCriticalRemindersAsync.
            var parts = marker.TextRu.Split(';');
            if (parts.Length != 2) continue;

            var thresholdPart = parts[0].Split('=');
            var duePart = parts[1].Split('=');
            if (thresholdPart.Length != 2 || duePart.Length != 2) continue;
            if (!int.TryParse(thresholdPart[1], out var threshold)) continue;
            if (!DateOnly.TryParse(duePart[1], out var dueDate)) continue;

            if (!result.TryGetValue(marker.EntityId, out var list))
            {
                list = [];
                result[marker.EntityId] = list;
            }

            list.Add((threshold, dueDate));
        }

        return result;
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

        if (!request.SendInApp && !request.SendEmail)
            throw new InvalidOperationException("Выберите хотя бы один канал отправки — в системе или по почте");

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

        // Канал выбирается явно (request.SendInApp/SendEmail, хотя бы один — проверено выше).
        // Раньше рассылка была только системным уведомлением без почты; теперь план, если
        // включён, готовится под оба канала сразу: как обычный файл системы для карточки
        // уведомления (AttachmentFileId, только если есть системное уведомление, которое его
        // покажет) и/или как вложение письма (MailAttachment, только если есть почта).
        int? attachmentFileId = null;
        MailAttachment? mailAttachment = null;

        if (request.IncludePlan)
        {
            var excelBytes = await _vndService.ExportActualizationPlanAsync(request.PlanExport!, languageCode);
            var fileName = $"План актуализации ({DateTime.UtcNow:dd.MM.yyyy}).xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            if (request.SendInApp)
            {
                var ownerId = currentUserId
                    ?? throw new InvalidOperationException("Не удалось определить пользователя для сохранения файла плана");

                var fileAttachment = await _fileStorage.SaveGeneratedAsync(excelBytes, fileName, contentType, ownerId);
                attachmentFileId = fileAttachment.Id;
            }

            if (request.SendEmail)
                mailAttachment = new MailAttachment {FileName = fileName, ContentType = contentType, Bytes = excelBytes};
        }

        if (request.SendInApp)
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = request.Subject.Trim(),
                BodyRu = request.Message.Trim(),
                Category = NotificationCategory.Vnd,
                Severity = NotificationSeverity.Info,
                Url = null,
                UserIds = recipientIds,
                AttachmentFileId = attachmentFileId,
                Attachment = mailAttachment,
                SkipEmail = !request.SendEmail,
            }, currentUserId);
        }
        else
        {
            // Только почта, без записи в системе — создавать уведомление не для чего, письмо
            // ставим в очередь напрямую.
            await _mail.EnqueueAsync(
                recipientIds, request.Subject.Trim(), request.Message.Trim(), null, null, mailAttachment);
        }

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
        MonthlyDigestNotifyInApp = s.MonthlyDigestNotifyInApp,
        MonthlyDigestNotifyEmail = s.MonthlyDigestNotifyEmail,
        CriticalRemindersEnabled = s.CriticalRemindersEnabled,
        CriticalReminderDays = ParseThresholdDays(s.CriticalReminderDaysCsv),
        CriticalRemindersNotifyInApp = s.CriticalRemindersNotifyInApp,
        CriticalRemindersNotifyEmail = s.CriticalRemindersNotifyEmail,
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
        var subject = $"{ActualizationNotificationMessages.MonthlyDigestSubjectPrefix}{today:MM.yyyy} — {orgUnitName}";

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
