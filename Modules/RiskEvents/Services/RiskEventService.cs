using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.RiskEvents.DTO;
using delosfera_server.Modules.RiskEvents.Models;

namespace delosfera_server.Modules.RiskEvents.Services;

public interface IRiskEventService
{
    /// <summary>
    /// Поднять событие операционного риска. Если источник задан участником этапа
    /// (RouteParticipantId) или самим этапом (RouteStepId), недостающие ссылки на
    /// этап, маршрут и документ выводятся автоматически.
    /// </summary>
    Task<RiskEventDetails> RaiseAsync(RaiseRiskEventRequest request, int initiatorUserId, CancellationToken ct = default);

    Task<RiskEventPage> SearchAsync(RiskEventFilter filter, CancellationToken ct = default);

    Task<RiskEventDetails?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Сменить статус события и (опционально) уточнить сумму прямых потерь.</summary>
    Task<RiskEventDetails> ChangeStatusAsync(int id, ChangeRiskEventStatusRequest request, int actorUserId, CancellationToken ct = default);
}

/// <summary>
/// Ведёт базу событий операционного риска (RISK-1, 716-П / НБКР).
///
/// Событие поднимается с любого шага согласования или карточки документа. Привязка к
/// шагу нужна, чтобы событие показывало не абстрактный «сбой процесса», а конкретное
/// место: какой документ, какой этап, чья задача. Дата события — по времени банка
/// (CLK-1), а не UTC: на UTC+6 событие вечера иначе получило бы вчерашнюю дату.
/// </summary>
public class RiskEventService : IRiskEventService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public RiskEventService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<RiskEventDetails> RaiseAsync(
        RaiseRiskEventRequest request, int initiatorUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("Описание события обязательно");

        if (request.DirectLossAmount is < 0)
            throw new InvalidOperationException("Сумма прямых потерь не может быть отрицательной");

        var documentId = request.DocumentId;
        var routeInstanceId = request.RouteInstanceId;
        var routeStepId = request.RouteStepId;
        var routeParticipantId = request.RouteParticipantId;

        // Источник задан задачей участника — вытягиваем этап, маршрут и документ по нему.
        if (routeParticipantId is int pid)
        {
            var participant = await _db.RouteParticipants
                .Include(p => p.RouteStep!).ThenInclude(s => s.RouteInstance)
                .SingleOrDefaultAsync(p => p.Id == pid, ct)
                ?? throw new KeyNotFoundException("Участник этапа не найден");

            routeStepId ??= participant.RouteStepId;
            routeInstanceId ??= participant.RouteStep?.RouteInstanceId;
            documentId ??= participant.RouteStep?.RouteInstance?.DocumentId;
        }
        // Источник задан этапом — вытягиваем маршрут и документ по нему.
        else if (routeStepId is int sid)
        {
            var step = await _db.RouteSteps
                .Include(s => s.RouteInstance)
                .SingleOrDefaultAsync(s => s.Id == sid, ct)
                ?? throw new KeyNotFoundException("Этап маршрута не найден");

            routeInstanceId ??= step.RouteInstanceId;
            documentId ??= step.RouteInstance?.DocumentId;
        }
        // Источник задан маршрутом — вытягиваем документ.
        else if (routeInstanceId is int iid)
        {
            var instance = await _db.RouteInstances
                .SingleOrDefaultAsync(i => i.Id == iid, ct)
                ?? throw new KeyNotFoundException("Маршрут не найден");

            documentId ??= instance.DocumentId;
        }

        var now = DateTime.UtcNow;
        var entity = new RiskEvent
        {
            Type = request.Type,
            Severity = request.Severity,
            Status = RiskEventStatus.New,
            Description = request.Description.Trim(),
            DocumentId = documentId,
            RouteInstanceId = routeInstanceId,
            RouteStepId = routeStepId,
            RouteParticipantId = routeParticipantId,
            InitiatorUserId = initiatorUserId,
            OccurredOn = request.OccurredOn ?? _clock.Today,
            DirectLossAmount = request.DirectLossAmount,
            LossCurrency = NormalizeCurrency(request.LossCurrency),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.RiskEvents.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("RiskEvent", entity.Id, "Raise", initiatorUserId, new
        {
            entity.Type,
            entity.Severity,
            entity.DocumentId,
            entity.RouteInstanceId,
            entity.RouteStepId,
            entity.RouteParticipantId,
            entity.DirectLossAmount,
            entity.LossCurrency,
        });

        return await GetAsync(entity.Id, ct) ?? throw new InvalidOperationException("Событие не найдено после создания");
    }

    public async Task<RiskEventPage> SearchAsync(RiskEventFilter filter, CancellationToken ct = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 50 : filter.PageSize;

        var q = _db.RiskEvents.AsNoTracking().AsQueryable();

        if (filter.Type is { } t) q = q.Where(e => e.Type == t);
        if (filter.Severity is { } sev) q = q.Where(e => e.Severity == sev);
        if (filter.Status is { } st) q = q.Where(e => e.Status == st);
        if (filter.DocumentId is { } docId) q = q.Where(e => e.DocumentId == docId);
        if (filter.InitiatorUserId is { } uid) q = q.Where(e => e.InitiatorUserId == uid);
        if (filter.From is { } from) q = q.Where(e => e.OccurredOn >= from);
        if (filter.To is { } to) q = q.Where(e => e.OccurredOn <= to);

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{filter.Query.Trim()}%";
            q = q.Where(e => EF.Functions.ILike(e.Description, pattern));
        }

        var total = await q.CountAsync(ct);

        // Метки типов/статусов считаются в памяти: их формулировки — это switch по
        // enum, который EF в SQL не переводит. Из базы тянем сырые поля с join'ами
        // на документ и инициатора, дальше раскладываем в DTO.
        var rows = await q
            .OrderByDescending(e => e.OccurredOn).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new RiskEventRow
            {
                Id = e.Id,
                Type = e.Type,
                Severity = e.Severity,
                Status = e.Status,
                OccurredOn = e.OccurredOn,
                Description = e.Description,
                DirectLossAmount = e.DirectLossAmount,
                LossCurrency = e.LossCurrency,
                DocumentId = e.DocumentId,
                DocumentTitle = e.Document != null ? e.Document.Title : null,
                RouteInstanceId = e.RouteInstanceId,
                RouteStepId = e.RouteStepId,
                RouteParticipantId = e.RouteParticipantId,
                InitiatorUserId = e.InitiatorUserId,
                InitiatorName = e.InitiatorUser != null ? e.InitiatorUser.FullName : null,
                ResolutionComment = e.ResolutionComment,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
            })
            .ToListAsync(ct);

        var items = rows.Select(ToListItem).ToList();

        return new RiskEventPage { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<RiskEventDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        var row = await _db.RiskEvents.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new RiskEventRow
            {
                Id = e.Id,
                Type = e.Type,
                Severity = e.Severity,
                Status = e.Status,
                OccurredOn = e.OccurredOn,
                Description = e.Description,
                DirectLossAmount = e.DirectLossAmount,
                LossCurrency = e.LossCurrency,
                DocumentId = e.DocumentId,
                DocumentTitle = e.Document != null ? e.Document.Title : null,
                RouteInstanceId = e.RouteInstanceId,
                RouteStepId = e.RouteStepId,
                RouteParticipantId = e.RouteParticipantId,
                InitiatorUserId = e.InitiatorUserId,
                InitiatorName = e.InitiatorUser != null ? e.InitiatorUser.FullName : null,
                ResolutionComment = e.ResolutionComment,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : ToDetails(row);
    }

    // ── Раскладка сырых строк в DTO (метки — в памяти) ─────────────────────────

    private sealed class RiskEventRow
    {
        public int Id { get; init; }
        public RiskEventType Type { get; init; }
        public RiskEventSeverity Severity { get; init; }
        public RiskEventStatus Status { get; init; }
        public DateOnly OccurredOn { get; init; }
        public string Description { get; init; } = "";
        public decimal? DirectLossAmount { get; init; }
        public string LossCurrency { get; init; } = "";
        public int? DocumentId { get; init; }
        public string? DocumentTitle { get; init; }
        public int? RouteInstanceId { get; init; }
        public int? RouteStepId { get; init; }
        public int? RouteParticipantId { get; init; }
        public int InitiatorUserId { get; init; }
        public string? InitiatorName { get; init; }
        public string? ResolutionComment { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private static RiskEventListItem ToListItem(RiskEventRow e) => new()
    {
        Id = e.Id,
        Type = e.Type.ToString(),
        TypeTitle = RiskEventLabels.Type(e.Type),
        Severity = e.Severity.ToString(),
        SeverityTitle = RiskEventLabels.Severity(e.Severity),
        Status = e.Status.ToString(),
        StatusTitle = RiskEventLabels.Status(e.Status),
        OccurredOn = e.OccurredOn,
        ShortDescription = e.Description.Length > 160 ? e.Description[..160] : e.Description,
        DirectLossAmount = e.DirectLossAmount,
        LossCurrency = e.LossCurrency,
        DocumentId = e.DocumentId,
        DocumentTitle = e.DocumentTitle,
        InitiatorUserId = e.InitiatorUserId,
        InitiatorName = e.InitiatorName,
        CreatedAt = e.CreatedAt,
    };

    private static RiskEventDetails ToDetails(RiskEventRow e) => new()
    {
        Id = e.Id,
        Type = e.Type.ToString(),
        TypeTitle = RiskEventLabels.Type(e.Type),
        Severity = e.Severity.ToString(),
        SeverityTitle = RiskEventLabels.Severity(e.Severity),
        Status = e.Status.ToString(),
        StatusTitle = RiskEventLabels.Status(e.Status),
        OccurredOn = e.OccurredOn,
        ShortDescription = e.Description.Length > 160 ? e.Description[..160] : e.Description,
        Description = e.Description,
        ResolutionComment = e.ResolutionComment,
        DirectLossAmount = e.DirectLossAmount,
        LossCurrency = e.LossCurrency,
        DocumentId = e.DocumentId,
        DocumentTitle = e.DocumentTitle,
        RouteInstanceId = e.RouteInstanceId,
        RouteStepId = e.RouteStepId,
        RouteParticipantId = e.RouteParticipantId,
        InitiatorUserId = e.InitiatorUserId,
        InitiatorName = e.InitiatorName,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    public async Task<RiskEventDetails> ChangeStatusAsync(
        int id, ChangeRiskEventStatusRequest request, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.RiskEvents.SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new KeyNotFoundException("Событие операционного риска не найдено");

        if (request.DirectLossAmount is < 0)
            throw new InvalidOperationException("Сумма прямых потерь не может быть отрицательной");

        var previous = entity.Status;
        entity.Status = request.Status;

        if (!string.IsNullOrWhiteSpace(request.Comment))
            entity.ResolutionComment = request.Comment.Trim();

        if (request.DirectLossAmount is { } loss)
        {
            entity.DirectLossAmount = loss;
            entity.LossCurrency = NormalizeCurrency(request.LossCurrency ?? entity.LossCurrency);
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("RiskEvent", entity.Id, "ChangeStatus", actorUserId, new
        {
            From = previous.ToString(),
            To = entity.Status.ToString(),
            entity.DirectLossAmount,
            entity.LossCurrency,
        });

        return await GetAsync(entity.Id, ct) ?? throw new InvalidOperationException("Событие не найдено после обновления");
    }

    /// <summary>Валюта — трёхбуквенный ISO в верхнем регистре; по умолчанию сом.</summary>
    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return "KGS";
        var trimmed = currency.Trim().ToUpperInvariant();
        return trimmed.Length > 3 ? trimmed[..3] : trimmed;
    }
}
