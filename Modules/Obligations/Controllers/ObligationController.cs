using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Obligations.Models;
using delosfera_server.Modules.Obligations.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Obligations.Controllers;

public class ObligationSaveRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Basis { get; set; }
    public ObligationKind Kind { get; set; } = ObligationKind.Other;
    public Periodicity Periodicity { get; set; } = Periodicity.Monthly;
    public MeetingBody? Body { get; set; }
    public int? ResponsibleUserId { get; set; }
    public int? ResponsibleUnitId { get; set; }
    public int GraceDays { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FulfilRequest
{
    public string? Comment { get; set; }
}

/// <summary>Карточка обязательства на доске (ПР-1).</summary>
public class ObligationBoardItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Responsible { get; set; }
    public string PeriodicityTitle { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public int MissedCount { get; set; }
}

/// <summary>Колонка доски: стадия текущего периода и обязательства на ней.</summary>
public class ObligationBoardColumn
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public int Count { get; set; }
    public List<ObligationBoardItem> Items { get; set; } = [];
}

public class WaiveRequest
{
    public string Reason { get; set; } = "";
}

/// <summary>
/// Регулярные обязательства: заседания комитетов, отчёты, пересмотр политик,
/// график сдачи в НБКР.
///
/// Регулятор мыслит периодичностью — «не реже раза в месяц», «ежеквартально». Этот
/// раздел отвечает на вопрос, соблюдается ли она, до проверки, а не на ней.
/// </summary>
[ApiController]
[Authorize]
[Route("api/obligations")]
[Tags("Регулярные обязательства")]
public class ObligationController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IObligationService _obligations;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ObligationController(
        DelosferaDbContext db, IObligationService obligations, ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _obligations = obligations;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Перечень обязательств с ближайшим сроком и состоянием текущего периода.</summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.RecurringObligations.AsNoTracking();
        if (!includeInactive) query = query.Where(o => o.IsActive);

        var rows = await query
            .OrderBy(o => o.Title)
            .Select(o => new
            {
                o.Id,
                o.Title,
                o.Description,
                o.Basis,
                Kind = o.Kind.ToString(),
                Periodicity = o.Periodicity.ToString(),
                Body = o.Body == null ? null : o.Body.ToString(),
                o.GraceDays,
                o.StartsOn,
                o.EndsOn,
                o.IsActive,
                Responsible = o.ResponsibleUser == null ? null : o.ResponsibleUser.FullName,
                ResponsibleUnit = o.ResponsibleUnit == null ? null : o.ResponsibleUnit.TitleRu,

                // Текущий период — тот, чей срок ещё не вышел и который ближе всех.
                Current = o.Periods
                    .Where(p => p.PeriodEnd >= today)
                    .OrderBy(p => p.PeriodStart)
                    .Select(p => new
                    {
                        p.Id, p.PeriodStart, p.PeriodEnd, p.DueDate,
                        Status = p.Status.ToString(),
                    })
                    .FirstOrDefault(),

                MissedCount = o.Periods.Count(p => p.Status == ObligationPeriodStatus.Missed),
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>
    /// Доска обязательств по стадии текущего периода (ПР-1): ожидает, просрочено,
    /// исполнено, снято. Отвечает на вопрос «что горит», не открывая каждую карточку.
    /// </summary>
    [HttpGet("board")]
    public async Task<IActionResult> Board(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await _db.RecurringObligations.AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Title)
            .Select(o => new
            {
                o.Id,
                o.Title,
                o.Periodicity,
                Responsible = o.ResponsibleUser == null ? null : o.ResponsibleUser.FullName,
                Current = o.Periods
                    .Where(p => p.PeriodEnd >= today)
                    .OrderBy(p => p.PeriodStart)
                    .Select(p => new { p.DueDate, p.Status })
                    .FirstOrDefault(),
                MissedCount = o.Periods.Count(p => p.Status == ObligationPeriodStatus.Missed),
            })
            .ToListAsync(ct);

        // Ключ колонки — стадия текущего периода. Без текущего периода считаем, что
        // обязательство ждёт открытия следующего: место ему в «Ожидает».
        var items = rows.Select(o =>
        {
            var status = o.Current?.Status ?? ObligationPeriodStatus.Pending;
            return new
            {
                Status = status,
                Item = new ObligationBoardItem
                {
                    Id = o.Id,
                    Title = o.Title,
                    Responsible = o.Responsible,
                    PeriodicityTitle = PeriodicityTitle(o.Periodicity),
                    DueDate = o.Current?.DueDate,
                    IsOverdue = status == ObligationPeriodStatus.Pending
                                && o.Current is { } c && c.DueDate < today,
                    MissedCount = o.MissedCount,
                },
            };
        }).ToList();

        // Порядок колонок — от того, что требует действия, к закрытому.
        var order = new[]
        {
            ObligationPeriodStatus.Pending,
            ObligationPeriodStatus.Missed,
            ObligationPeriodStatus.Fulfilled,
            ObligationPeriodStatus.Waived,
        };

        var columns = order.Select(st =>
        {
            var group = items.Where(i => i.Status == st)
                .Select(i => i.Item)
                .OrderByDescending(i => i.IsOverdue)
                .ThenBy(i => i.DueDate ?? DateOnly.MaxValue)
                .ToList();
            return new ObligationBoardColumn
            {
                Code = st.ToString(),
                Title = PeriodStatusTitle(st),
                Count = group.Count,
                Items = group,
            };
        }).ToList();

        return Ok(columns);
    }

    /// <summary>Выгрузка реестра обязательств в Excel с состоянием текущего периода (ЭК-3).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.RecurringObligations.AsNoTracking();
        if (!includeInactive) query = query.Where(o => o.IsActive);

        var rows = await query
            .OrderBy(o => o.Title)
            .Select(o => new
            {
                o.Title,
                o.Kind,
                o.Periodicity,
                o.StartsOn,
                o.EndsOn,
                Responsible = o.ResponsibleUser == null ? null : o.ResponsibleUser.FullName,
                ResponsibleUnit = o.ResponsibleUnit == null ? null : o.ResponsibleUnit.TitleRu,
                CurrentDue = o.Periods
                    .Where(p => p.PeriodEnd >= today)
                    .OrderBy(p => p.PeriodStart)
                    .Select(p => (DateOnly?)p.DueDate)
                    .FirstOrDefault(),
                CurrentStatus = o.Periods
                    .Where(p => p.PeriodEnd >= today)
                    .OrderBy(p => p.PeriodStart)
                    .Select(p => (ObligationPeriodStatus?)p.Status)
                    .FirstOrDefault(),
                MissedCount = o.Periods.Count(p => p.Status == ObligationPeriodStatus.Missed),
            })
            .ToListAsync(ct);

        var sheet = new Common.Export.XlsxSheet
        {
            Name = "Реестр обязательств",
            Header = ["Обязательство", "Вид", "Периодичность", "Ответственный", "Подразделение", "Начало", "Окончание", "Текущий срок", "Статус периода", "Пропущено"],
            Widths = [46, 24, 16, 26, 28, 12, 12, 14, 18, 12],
            Rows = rows.Select(o => new[]
            {
                o.Title,
                ObligationKindTitle(o.Kind),
                PeriodicityTitle(o.Periodicity),
                o.Responsible ?? "—",
                o.ResponsibleUnit ?? "—",
                o.StartsOn.ToString("dd.MM.yyyy"),
                o.EndsOn?.ToString("dd.MM.yyyy") ?? "—",
                o.CurrentDue?.ToString("dd.MM.yyyy") ?? "—",
                o.CurrentStatus is { } st ? PeriodStatusTitle(st) : "—",
                o.MissedCount.ToString(),
            }).ToList(),
        };

        var bytes = Common.Export.XlsxWorkbook.Build(sheet);
        var stamp = DateTime.Now.ToString("dd.MM.yyyy");
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Реестр обязательств {stamp}.xlsx");
    }

    private static string ObligationKindTitle(ObligationKind kind) => kind switch
    {
        ObligationKind.MeetingHeld => "Проведение заседания",
        ObligationKind.ReportSubmitted => "Сдача отчёта",
        ObligationKind.DocumentReviewed => "Пересмотр документа",
        _ => "Иное",
    };

    private static string PeriodicityTitle(Periodicity p) => p switch
    {
        Periodicity.Weekly => "Еженедельно",
        Periodicity.Monthly => "Ежемесячно",
        Periodicity.Quarterly => "Ежеквартально",
        Periodicity.SemiAnnual => "Раз в полугодие",
        Periodicity.Annual => "Ежегодно",
        _ => p.ToString(),
    };

    private static string PeriodStatusTitle(ObligationPeriodStatus s) => s switch
    {
        ObligationPeriodStatus.Pending => "Ожидает",
        ObligationPeriodStatus.Fulfilled => "Исполнено",
        ObligationPeriodStatus.Missed => "Пропущено",
        ObligationPeriodStatus.Waived => "Снято",
        _ => s.ToString(),
    };

    /// <summary>Периоды одного обязательства — история исполнения.</summary>
    [HttpGet("{id:int}/periods")]
    public async Task<IActionResult> Periods(int id, CancellationToken ct)
    {
        var rows = await _db.ObligationPeriods.AsNoTracking()
            .Where(p => p.ObligationId == id)
            .OrderByDescending(p => p.PeriodStart)
            .Select(p => new
            {
                p.Id, p.PeriodStart, p.PeriodEnd, p.DueDate,
                Status = p.Status.ToString(),
                p.FulfilledAt,
                FulfilledBy = p.FulfilledByUser == null ? null : p.FulfilledByUser.FullName,
                p.MeetingId,
                p.Comment,
                IsLate = p.Status == ObligationPeriodStatus.Fulfilled
                         && p.FulfilledAt != null
                         && DateOnly.FromDateTime(p.FulfilledAt.Value) > p.DueDate,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>Что просрочено и что горит — сводка по всем обязательствам.</summary>
    [HttpGet("attention")]
    public async Task<IActionResult> Attention([FromQuery] int days = 14, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var edge = today.AddDays(Math.Clamp(days, 1, 180));

        var rows = await _db.ObligationPeriods.AsNoTracking()
            .Include(p => p.Obligation)
            .Where(p => p.Status == ObligationPeriodStatus.Missed
                        || (p.Status == ObligationPeriodStatus.Pending && p.DueDate <= edge))
            .OrderBy(p => p.DueDate)
            .Select(p => new
            {
                PeriodId = p.Id,
                p.ObligationId,
                Title = p.Obligation!.Title,
                Basis = p.Obligation.Basis,
                Periodicity = p.Obligation.Periodicity.ToString(),
                Responsible = p.Obligation.ResponsibleUser == null
                    ? null
                    : p.Obligation.ResponsibleUser.FullName,
                p.PeriodStart,
                p.PeriodEnd,
                p.DueDate,
                Status = p.Status.ToString(),
                DaysLeft = p.DueDate.DayNumber - today.DayNumber,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpPost]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Create([FromBody] ObligationSaveRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new {message = error});

        var now = DateTime.UtcNow;

        var obligation = new RecurringObligation
        {
            Title = request.Title.Trim(),
            Description = Trim(request.Description),
            Basis = Trim(request.Basis),
            Kind = request.Kind,
            Periodicity = request.Periodicity,
            Body = request.Kind == ObligationKind.MeetingHeld ? request.Body : null,
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleUnitId = request.ResponsibleUnitId,
            GraceDays = Math.Clamp(request.GraceDays, 0, 180),
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.RecurringObligations.Add(obligation);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Obligation", obligation.Id, "Created", _currentUser.UserId,
            new {obligation.Title, Periodicity = obligation.Periodicity.ToString()});

        // Сразу заводим периоды, чтобы обязательство не выглядело пустым до
        // ближайшего срабатывания фоновой службы.
        await _obligations.SyncAsync(ct);

        return Ok(new {id = obligation.Id});
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Update(int id, [FromBody] ObligationSaveRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new {message = error});

        var obligation = await _db.RecurringObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (obligation is null) return NotFound();

        var previousPeriodicity = obligation.Periodicity;

        obligation.Title = request.Title.Trim();
        obligation.Description = Trim(request.Description);
        obligation.Basis = Trim(request.Basis);
        obligation.Kind = request.Kind;
        obligation.Periodicity = request.Periodicity;
        obligation.Body = request.Kind == ObligationKind.MeetingHeld ? request.Body : null;
        obligation.ResponsibleUserId = request.ResponsibleUserId;
        obligation.ResponsibleUnitId = request.ResponsibleUnitId;
        obligation.GraceDays = Math.Clamp(request.GraceDays, 0, 180);
        obligation.StartsOn = request.StartsOn;
        obligation.EndsOn = request.EndsOn;
        obligation.IsActive = request.IsActive;
        obligation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Obligation", obligation.Id, "Updated", _currentUser.UserId);

        if (previousPeriodicity != obligation.Periodicity)
            await _audit.LogAsync("Obligation", obligation.Id, "PeriodicityChanged", _currentUser.UserId,
                new {From = previousPeriodicity.ToString(), To = obligation.Periodicity.ToString()});

        return Ok();
    }

    /// <summary>Отметить период исполненным. Для заседаний недоступно — они закрываются сами.</summary>
    [HttpPost("periods/{periodId:int}/fulfil")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Fulfil(int periodId, [FromBody] FulfilRequest request, CancellationToken ct)
    {
        try
        {
            await _obligations.FulfilAsync(periodId, request.Comment, _currentUser.UserId, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return Conflict(new {message = ex.Message}); }
    }

    /// <summary>Снять период: в этом промежутке обязательство не требовалось.</summary>
    [HttpPost("periods/{periodId:int}/waive")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Waive(int periodId, [FromBody] WaiveRequest request, CancellationToken ct)
    {
        try
        {
            await _obligations.WaiveAsync(periodId, request.Reason, _currentUser.UserId, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    private static string? Validate(ObligationSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Укажите название обязательства.";

        if (request.Kind == ObligationKind.MeetingHeld && request.Body is null)
            return "Для обязательства о заседании укажите орган.";

        if (request.EndsOn is {} ends && ends < request.StartsOn)
            return "Дата окончания раньше даты начала.";

        return null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
