using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IActualizationPlanService
{
    Task<List<int>> YearsAsync();
    Task<PlanDto?> GetAsync(int year);
    Task<PlanDto> CreateAsync(PlanCreateRequest request, int userId);
    Task<PlanDto> ApproveAsync(int planId, PlanApproveRequest request, int userId);

    Task<PlanItemDto> AddItemAsync(int planId, PlanItemSaveRequest request, int userId);
    Task<PlanItemDto> UpdateItemAsync(int itemId, PlanItemSaveRequest request, int userId);
    Task<PlanItemDto> RescheduleAsync(int itemId, PlanItemRescheduleRequest request, int userId);
    Task<PlanItemDto> ExcludeAsync(int itemId, PlanItemExcludeRequest request, int userId);
    Task<List<PlanItemEventDto>> HistoryAsync(int itemId);

    Task<ActualizationSettingsDto> GetSettingsAsync();
    Task<ActualizationSettingsDto> SaveSettingsAsync(ActualizationSettingsDto request);
}

/// <summary>
/// Годовой план актуализации ВНД (PLN-01..03, PLN-06).
///
/// Срок и его цвет считаются, а не хранятся: пороги настраиваются Отделом
/// методологии, и сохранённый цвет устарел бы в день их правки.
///
/// Каждое изменение позиции пишется в её журнал (PLN-07): методологу нужна история
/// конкретной строки плана — кто перенёс срок и почему, — а не выборка из общего аудита.
/// </summary>
public class ActualizationPlanService : IActualizationPlanService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public ActualizationPlanService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<int>> YearsAsync() =>
        await _db.ActualizationPlans.OrderByDescending(p => p.Year).Select(p => p.Year).ToListAsync();

    public async Task<PlanDto?> GetAsync(int year)
    {
        var plan = await LoadByYearAsync(year);
        return plan is null ? null : await BuildAsync(plan);
    }

    public async Task<PlanDto> CreateAsync(PlanCreateRequest request, int userId)
    {
        var year = request.Year > 0 ? request.Year : _clock.Today.Year;

        if (await _db.ActualizationPlans.AnyAsync(p => p.Year == year))
            throw new InvalidOperationException($"План актуализации на {year} год уже заведён");

        var plan = new ActualizationPlan {Year = year};
        _db.ActualizationPlans.Add(plan);
        await _db.SaveChangesAsync();

        return await BuildAsync((await LoadByYearAsync(year))!);
    }

    public async Task<PlanDto> ApproveAsync(int planId, PlanApproveRequest request, int userId)
    {
        var plan = await _db.ActualizationPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new KeyNotFoundException("План не найден");

        if (string.IsNullOrWhiteSpace(request.ApprovalNote))
            throw new InvalidOperationException("Укажите, чем утверждён план");

        if (plan.Items.Count == 0)
            throw new InvalidOperationException("План без позиций не утверждается");

        plan.Status = ActualizationPlanStatus.Approved;
        plan.ApprovalNote = request.ApprovalNote.Trim();
        plan.ApprovedOn = _clock.Today;

        await _db.SaveChangesAsync();

        return await BuildAsync((await LoadByYearAsync(plan.Year))!);
    }

    public async Task<PlanItemDto> AddItemAsync(int planId, PlanItemSaveRequest request, int userId)
    {
        var plan = await _db.ActualizationPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId)
            ?? throw new KeyNotFoundException("План не найден");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Укажите наименование ВНД");

        if (request.DueDate == default)
            throw new InvalidOperationException("Укажите плановую дату актуализации");

        var item = new ActualizationPlanItem
        {
            PlanId = plan.Id,
            Order = plan.Items.Count == 0 ? 1 : plan.Items.Max(i => i.Order) + 1,
            Title = request.Title.Trim(),
            VndDocumentId = request.VndDocumentId,
            ResponsibleUnitId = request.ResponsibleUnitId,
            CuratorUserId = request.CuratorUserId,
            ApprovalBodyId = request.ApprovalBodyId,
            DueDate = request.DueDate,
            Comment = request.Comment?.Trim(),
        };

        _db.ActualizationPlanItems.Add(item);
        await _db.SaveChangesAsync();

        await LogAsync(item.Id, "Created", $"Позиция добавлена в план, срок {item.DueDate:dd.MM.yyyy}", userId);

        return await LoadItemDtoAsync(item.Id);
    }

    public async Task<PlanItemDto> UpdateItemAsync(int itemId, PlanItemSaveRequest request, int userId)
    {
        var item = await LoadItemAsync(itemId);

        if (!string.IsNullOrWhiteSpace(request.Title)) item.Title = request.Title.Trim();

        item.VndDocumentId = request.VndDocumentId;
        item.ResponsibleUnitId = request.ResponsibleUnitId;
        item.CuratorUserId = request.CuratorUserId;
        item.ApprovalBodyId = request.ApprovalBodyId;
        item.Comment = request.Comment?.Trim();

        // Срок правится отдельной операцией: у переноса обязательна причина, и
        // спрятать его в общем сохранении значит потерять след в журнале.
        await _db.SaveChangesAsync();
        await LogAsync(itemId, "Updated", "Изменены реквизиты позиции", userId);

        return await LoadItemDtoAsync(itemId);
    }

    public async Task<PlanItemDto> RescheduleAsync(int itemId, PlanItemRescheduleRequest request, int userId)
    {
        var item = await LoadItemAsync(itemId);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Укажите причину переноса срока");

        if (request.DueDate == default)
            throw new InvalidOperationException("Укажите новый срок");

        var previous = item.DueDate;
        item.DueDate = request.DueDate;

        await _db.SaveChangesAsync();
        await LogAsync(itemId, "Rescheduled",
            $"Срок перенесён с {previous:dd.MM.yyyy} на {request.DueDate:dd.MM.yyyy}. Причина: {request.Reason.Trim()}",
            userId);

        return await LoadItemDtoAsync(itemId);
    }

    public async Task<PlanItemDto> ExcludeAsync(int itemId, PlanItemExcludeRequest request, int userId)
    {
        var item = await LoadItemAsync(itemId);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Укажите причину исключения позиции");

        item.Status = PlanItemStatus.Excluded;
        item.Comment = request.Reason.Trim();

        await _db.SaveChangesAsync();
        await LogAsync(itemId, "Excluded", $"Позиция снята с плана. Причина: {request.Reason.Trim()}", userId);

        return await LoadItemDtoAsync(itemId);
    }

    public async Task<List<PlanItemEventDto>> HistoryAsync(int itemId)
    {
        var events = await _db.PlanItemEvents
            .Include(e => e.User)
            .Where(e => e.PlanItemId == itemId)
            .OrderByDescending(e => e.At)
            .AsNoTracking()
            .ToListAsync();

        return events.Select(e => new PlanItemEventDto
        {
            Id = e.Id,
            Kind = e.Kind,
            Description = e.Description,
            UserName = e.User?.FullName,
            At = e.At,
        }).ToList();
    }

    public async Task<ActualizationSettingsDto> GetSettingsAsync()
    {
        var settings = await LoadSettingsAsync();

        return new ActualizationSettingsDto
        {
            GreenThresholdDays = settings.GreenThresholdDays,
            RedThresholdDays = settings.RedThresholdDays,
            CriticalReminderDays = settings.CriticalReminderDays,
            MonthlyDigestEnabled = settings.MonthlyDigestEnabled,
        };
    }

    public async Task<ActualizationSettingsDto> SaveSettingsAsync(ActualizationSettingsDto request)
    {
        if (request.RedThresholdDays >= request.GreenThresholdDays)
            throw new InvalidOperationException(
                "Красный порог должен быть меньше зелёного: иначе жёлтой зоны не остаётся");

        if (request.RedThresholdDays < 0 || request.CriticalReminderDays < 0)
            throw new InvalidOperationException("Пороги задаются в днях и не бывают отрицательными");

        var settings = await LoadSettingsAsync();

        settings.GreenThresholdDays = request.GreenThresholdDays;
        settings.RedThresholdDays = request.RedThresholdDays;
        settings.CriticalReminderDays = request.CriticalReminderDays;
        settings.MonthlyDigestEnabled = request.MonthlyDigestEnabled;

        await _db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<ActualizationSettings> LoadSettingsAsync()
    {
        var settings = await _db.ActualizationSettings.FirstOrDefaultAsync();
        if (settings is not null) return settings;

        // Значения по умолчанию засеяны миграцией; страховка на случай пустой таблицы.
        settings = new ActualizationSettings();
        _db.ActualizationSettings.Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private async Task<ActualizationPlan?> LoadByYearAsync(int year) =>
        await _db.ActualizationPlans
            .Include(p => p.Items).ThenInclude(i => i.VndDocument)
            .Include(p => p.Items).ThenInclude(i => i.ResponsibleUnit)
            .Include(p => p.Items).ThenInclude(i => i.Curator)
            .Include(p => p.Items).ThenInclude(i => i.ApprovalBody)
            .FirstOrDefaultAsync(p => p.Year == year);

    private async Task<ActualizationPlanItem> LoadItemAsync(int itemId) =>
        await _db.ActualizationPlanItems.FirstOrDefaultAsync(i => i.Id == itemId)
        ?? throw new KeyNotFoundException("Позиция плана не найдена");

    private async Task<PlanItemDto> LoadItemDtoAsync(int itemId)
    {
        var item = await _db.ActualizationPlanItems
            .Include(i => i.VndDocument)
            .Include(i => i.ResponsibleUnit)
            .Include(i => i.Curator)
            .Include(i => i.ApprovalBody)
            .AsNoTracking()
            .FirstAsync(i => i.Id == itemId);

        var settings = await LoadSettingsAsync();
        return ToDto(item, settings, _clock.Today);
    }

    private async Task<PlanDto> BuildAsync(ActualizationPlan plan)
    {
        var settings = await LoadSettingsAsync();
        var today = _clock.Today;

        var items = plan.Items
            .OrderBy(i => i.Order)
            .Select(i => ToDto(i, settings, today))
            .ToList();

        return new PlanDto
        {
            Id = plan.Id,
            Year = plan.Year,
            Status = plan.Status,
            StatusTitle = StatusTitle(plan.Status),
            ApprovalNote = plan.ApprovalNote,
            ApprovedOn = plan.ApprovedOn,
            Items = items,
            Total = items.Count,
            Green = items.Count(i => i.Urgency == PlanItemUrgency.Green),
            Yellow = items.Count(i => i.Urgency == PlanItemUrgency.Yellow),
            Red = items.Count(i => i.Urgency == PlanItemUrgency.Red),
            Done = items.Count(i => i.Urgency == PlanItemUrgency.Done),
            Unmatched = items.Count(i => i.IsUnmatched),
        };
    }

    internal static PlanItemDto ToDto(ActualizationPlanItem item, ActualizationSettings settings, DateOnly today)
    {
        var daysLeft = item.DueDate.DayNumber - today.DayNumber;

        return new PlanItemDto
        {
            Id = item.Id,
            PlanId = item.PlanId,
            Order = item.Order,
            Title = item.Title,
            VndDocumentId = item.VndDocumentId,
            VndCode = item.VndDocument?.Code,
            ResponsibleUnitId = item.ResponsibleUnitId,
            ResponsibleUnitTitle = item.ResponsibleUnit?.TitleRu,
            CuratorUserId = item.CuratorUserId,
            CuratorName = item.Curator?.FullName,
            ApprovalBodyId = item.ApprovalBodyId,
            ApprovalBodyTitle = item.ApprovalBody?.TitleRu,
            DueDate = item.DueDate,
            NextDueDate = item.NextDueDate,
            StartedOn = item.StartedOn,
            CompletedOn = item.CompletedOn,
            Status = item.Status,
            StatusTitle = ItemStatusTitle(item.Status),
            Urgency = Urgency(item, settings, daysLeft),
            DaysLeft = daysLeft,
            Comment = item.Comment,
            IsUnmatched = item.VndDocumentId is null,
        };
    }

    /// <summary>
    /// Светофор методологии (PLN-03). Завершённые и снятые позиции из индикации
    /// выпадают: держать их красными — значит показывать тревогу там, где работа сделана.
    /// </summary>
    private static PlanItemUrgency Urgency(
        ActualizationPlanItem item, ActualizationSettings settings, int daysLeft)
    {
        if (item.Status is PlanItemStatus.Actual or PlanItemStatus.Excluded) return PlanItemUrgency.Done;

        if (daysLeft < 0 || daysLeft <= settings.RedThresholdDays) return PlanItemUrgency.Red;
        if (daysLeft <= settings.GreenThresholdDays) return PlanItemUrgency.Yellow;

        return PlanItemUrgency.Green;
    }

    private async Task LogAsync(int itemId, string kind, string description, int? userId)
    {
        _db.PlanItemEvents.Add(new PlanItemEvent
        {
            PlanItemId = itemId,
            Kind = kind,
            Description = description,
            UserId = userId,
            At = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }

    internal static string StatusTitle(ActualizationPlanStatus status) => status switch
    {
        ActualizationPlanStatus.Draft => "Формируется",
        ActualizationPlanStatus.Approved => "Утверждён",
        ActualizationPlanStatus.Closed => "Закрыт",
        _ => status.ToString(),
    };

    internal static string ItemStatusTitle(PlanItemStatus status) => status switch
    {
        PlanItemStatus.Planned => "Запланировано",
        PlanItemStatus.OnActualization => "На актуализации",
        PlanItemStatus.Actual => "Актуально",
        PlanItemStatus.Excluded => "Снято с плана",
        _ => status.ToString(),
    };
}
