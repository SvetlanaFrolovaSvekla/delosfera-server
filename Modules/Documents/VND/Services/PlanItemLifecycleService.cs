using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IPlanItemLifecycleService
{
    /// <summary>Запустить актуализацию по позиции плана — кнопка «Создать ТИД» (PLN-05).</summary>
    Task<PlanItemDto> StartActualizationAsync(int itemId, StartActualizationRequest request, int userId);
}

/// <summary>
/// Реакция плана на события цикла актуализации (PLN-06).
///
/// Вынесено отдельным интерфейсом, чтобы контур актуализации о плане только
/// сообщал, но не зависел от него: обратная зависимость замкнула бы сервисы в кольцо.
/// </summary>
public interface IPlanItemSync
{
    /// <summary>По ВНД началась работа: позиция переходит в «На актуализации».</summary>
    Task OnVndActualizationStartedAsync(int vndDocumentId, int userId);

    /// <summary>Изменения утверждены: позиция закрывается и получает новый срок.</summary>
    Task OnVndActualizationPublishedAsync(int vndDocumentId, int userId);
}

/// <summary>
/// Связь позиции плана с циклом актуализации ВНД (PLN-05, PLN-06).
///
/// Отдельного документа «ТИД» в системе нет: изменения ведутся циклом актуализации
/// самой ВНД, поэтому кнопка плана запускает его, а не создаёт вторую сущность с
/// теми же данными.
///
/// Статусы позиции переставляет система, а не сотрудник: план должен показывать
/// фактическое положение дел, а не то, что кто-то не забыл отметить галочку.
/// </summary>
public class PlanItemLifecycleService : IPlanItemLifecycleService
{
    private readonly DelosferaDbContext _db;
    private readonly IVndActualizationService _actualization;
    private readonly IBankClock _clock;
    private readonly ILogger<PlanItemLifecycleService> _logger;

    public PlanItemLifecycleService(
        DelosferaDbContext db,
        IVndActualizationService actualization,
        IBankClock clock,
        ILogger<PlanItemLifecycleService> logger)
    {
        _db = db;
        _actualization = actualization;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PlanItemDto> StartActualizationAsync(
        int itemId, StartActualizationRequest request, int userId)
    {
        var item = await _db.ActualizationPlanItems
            .Include(i => i.VndDocument)
            .FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Позиция плана не найдена");

        if (item.VndDocumentId is null)
            throw new InvalidOperationException(
                "Позиция не сопоставлена с документом базы ВНД — привяжите документ, " +
                "иначе изменения некуда вносить");

        if (item.Status == PlanItemStatus.Excluded)
            throw new InvalidOperationException("Позиция снята с плана");

        if (item.Status == PlanItemStatus.OnActualization)
            throw new InvalidOperationException("По позиции уже идёт актуализация");

        // Статус позиции и запись журнала ставит IPlanItemSync: контур актуализации
        // сообщает ему о старте сам, и дублировать это здесь — значит получить две
        // одинаковые записи в журнале и разъезд при запуске актуализации со стороны ВНД.
        await _actualization.StartAsync(item.VndDocumentId.Value, request, userId);

        return await LoadDtoAsync(item.Id);
    }

    private async Task<PlanItemDto> LoadDtoAsync(int itemId)
    {
        var item = await _db.ActualizationPlanItems
            .Include(i => i.VndDocument)
            .Include(i => i.ResponsibleUnit)
            .Include(i => i.Curator)
            .Include(i => i.ApprovalBody)
            .AsNoTracking()
            .FirstAsync(i => i.Id == itemId);

        var settings = await _db.ActualizationSettings.FirstOrDefaultAsync() ?? new ActualizationSettings();
        return ActualizationPlanService.ToDto(item, settings, _clock.Today);
    }
}

/// <summary>
/// Перевод позиций плана вслед за циклом актуализации ВНД (PLN-06).
///
/// Статусы переставляет система, а не сотрудник: план должен показывать фактическое
/// положение дел, а не то, что кто-то не забыл отметить галочку.
/// </summary>
public class PlanItemSyncService : IPlanItemSync
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;
    private readonly ILogger<PlanItemSyncService> _logger;

    public PlanItemSyncService(DelosferaDbContext db, IBankClock clock, ILogger<PlanItemSyncService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task OnVndActualizationStartedAsync(int vndDocumentId, int userId)
    {
        var items = await OpenItemsAsync(vndDocumentId);

        foreach (var item in items.Where(i => i.Status == PlanItemStatus.Planned))
        {
            item.Status = PlanItemStatus.OnActualization;
            item.StartedOn ??= _clock.Today;

            await LogAsync(item.Id, "ActualizationStarted",
                "Позиция переведена в «На актуализации»: по ВНД начат цикл изменений", userId);
        }

        await _db.SaveChangesAsync();
    }

    public async Task OnVndActualizationPublishedAsync(int vndDocumentId, int userId)
    {
        var items = await OpenItemsAsync(vndDocumentId);
        if (items.Count == 0) return;

        var vnd = await _db.VndDocuments.FirstOrDefaultAsync(v => v.Id == vndDocumentId);
        var today = _clock.Today;

        foreach (var item in items)
        {
            item.Status = PlanItemStatus.Actual;
            item.CompletedOn = today;

            // Новый срок берём из карточки ВНД: там его считает контур актуализации
            // по периоду документа. Дублировать эту логику в плане — значит однажды
            // получить в плане одну дату, а в карточке другую.
            item.NextDueDate = vnd?.DueActualizationDate;

            var next = item.NextDueDate is { } date
                ? $", следующая актуализация {date:dd.MM.yyyy}"
                : ", срок следующей актуализации не задан в карточке ВНД";

            await LogAsync(item.Id, "Completed",
                $"Изменения утверждены{next}", userId);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "План актуализации: по ВНД {VndId} закрыто позиций {Count}", vndDocumentId, items.Count);
    }

    /// <summary>
    /// Незакрытые позиции по документу. Позиция ищется в любом плане, а не только
    /// в текущем году: цикл актуализации может завершиться уже в следующем году,
    /// и позиция прошлого года всё равно должна закрыться.
    /// </summary>
    private async Task<List<ActualizationPlanItem>> OpenItemsAsync(int vndDocumentId) =>
        await _db.ActualizationPlanItems
            .Where(i => i.VndDocumentId == vndDocumentId)
            .Where(i => i.Status == PlanItemStatus.Planned || i.Status == PlanItemStatus.OnActualization)
            .ToListAsync();

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

        await Task.CompletedTask;
    }
}
