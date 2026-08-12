using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IPlanService
{
    Task<List<int>> YearsAsync();
    Task<PlanDto?> GetAsync(int year);
    Task<PlanDto> CreateAsync(PlanCreateRequest request, int actorUserId);
    Task<PlanDto> AddItemAsync(int planId, PlanItemRequest request, int actorUserId);
    Task<PlanDto> RemoveItemAsync(int itemId, int actorUserId);
    Task<PlanDto> ApproveAsync(int planId, PlanApproveRequest request, int actorUserId);
}

/// <summary>
/// Годовой План закупок и отчёт об исполнении (PRC-22).
///
/// Факт исполнения не хранится отдельным полем, а считается из заявок, сославшихся
/// на позицию: иначе план и реальность расходились бы при каждой правке заявки.
/// Заявка ссылается строкой PlanItem, поэтому сопоставление идёт по коду позиции —
/// так работает и ссылка «п. 4.2», которую инициатор пишет руками.
/// </summary>
public class PlanService : IPlanService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public PlanService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<int>> YearsAsync() =>
        await _db.ProcurementPlans.OrderByDescending(p => p.Year).Select(p => p.Year).ToListAsync();

    public async Task<PlanDto?> GetAsync(int year)
    {
        var plan = await Query().FirstOrDefaultAsync(p => p.Year == year);
        return plan is null ? null : await BuildAsync(plan);
    }

    public async Task<PlanDto> CreateAsync(PlanCreateRequest request, int actorUserId)
    {
        var year = request.Year > 0 ? request.Year : _clock.Today.Year;

        if (await _db.ProcurementPlans.AnyAsync(p => p.Year == year))
            throw new InvalidOperationException($"План закупок на {year} год уже заведён");

        var plan = new ProcurementPlan {Year = year};
        _db.ProcurementPlans.Add(plan);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementPlan", plan.Id, "Created", actorUserId, new {year});
        return await BuildAsync((await Query().FirstAsync(p => p.Id == plan.Id)));
    }

    public async Task<PlanDto> AddItemAsync(int planId, PlanItemRequest request, int actorUserId)
    {
        var plan = await Query().FirstOrDefaultAsync(p => p.Id == planId)
                   ?? throw new KeyNotFoundException("План закупок не найден");

        if (plan.Status == PlanStatus.Closed)
            throw new InvalidOperationException("Год закрыт — позиции не добавляются");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Укажите номер позиции плана");

        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Укажите предмет закупки");

        if (request.PlannedAmount <= 0)
            throw new ArgumentException("Укажите плановую сумму");

        if (request.Quarter is { } q && q is < 1 or > 4)
            throw new ArgumentException("Квартал указывается числом от 1 до 4");

        if (plan.Items.Any(i => i.Code.Equals(request.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Позиция «{request.Code}» в плане уже есть");

        _db.ProcurementPlanItems.Add(new ProcurementPlanItem
        {
            PlanId = planId,
            Code = request.Code.Trim(),
            Subject = request.Subject.Trim(),
            PlannedAmount = request.PlannedAmount,
            Quarter = request.Quarter,
            OrgUnitId = request.OrgUnitId,
            SubjectKind = request.SubjectKind,
            Note = request.Note?.Trim(),
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementPlan", planId, "ItemAdded", actorUserId, new
        {
            request.Code,
            request.PlannedAmount,
        });

        return await BuildAsync(await Query().FirstAsync(p => p.Id == planId));
    }

    public async Task<PlanDto> RemoveItemAsync(int itemId, int actorUserId)
    {
        var item = await _db.ProcurementPlanItems.Include(i => i.Plan)
                       .FirstOrDefaultAsync(i => i.Id == itemId)
                   ?? throw new KeyNotFoundException("Позиция плана не найдена");

        if (item.Plan!.Status == PlanStatus.Approved)
            throw new InvalidOperationException(
                "План утверждён Правлением — позиция исключается только корректировкой плана");

        // Позиция, на которую уже сослались заявки, не удаляется: иначе закупки
        // задним числом станут внеплановыми.
        var used = await _db.ProcurementRequests.CountAsync(r => r.PlanItem != null
                                                                 && r.PlanItem.Contains(item.Code));
        if (used > 0)
            throw new InvalidOperationException(
                $"На позицию «{item.Code}» ссылаются заявки ({used}) — удаление невозможно");

        var planId = item.PlanId;
        _db.ProcurementPlanItems.Remove(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementPlan", planId, "ItemRemoved", actorUserId, new {item.Code});

        return await BuildAsync(await Query().FirstAsync(p => p.Id == planId));
    }

    public async Task<PlanDto> ApproveAsync(int planId, PlanApproveRequest request, int actorUserId)
    {
        var plan = await Query().FirstOrDefaultAsync(p => p.Id == planId)
                   ?? throw new KeyNotFoundException("План закупок не найден");

        if (string.IsNullOrWhiteSpace(request.ApprovalProtocol))
            throw new ArgumentException("Укажите протокол Правления, которым утверждён план");

        if (plan.Items.Count == 0)
            throw new InvalidOperationException("В плане нет позиций — утверждать нечего");

        plan.Status = PlanStatus.Approved;
        plan.ApprovalProtocol = request.ApprovalProtocol.Trim();
        plan.ApprovedOn = request.ApprovedOn ?? _clock.Today;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementPlan", planId, "Approved", actorUserId, new
        {
            plan.ApprovalProtocol,
            plan.ApprovedOn,
            items = plan.Items.Count,
        });

        return await BuildAsync(await Query().FirstAsync(p => p.Id == planId));
    }

    private IQueryable<ProcurementPlan> Query() =>
        _db.ProcurementPlans
            .Include(p => p.Items.OrderBy(i => i.Code))
            .ThenInclude(i => i.OrgUnit);

    private async Task<PlanDto> BuildAsync(ProcurementPlan plan)
    {
        // Заявки года: факт исполнения плана считается по ним, а не хранится отдельно.
        var requests = await _db.ProcurementRequests
            .Where(r => r.CreatedAt.Year == plan.Year)
            .Select(r => new {r.Id, r.PlanItem, r.Amount})
            .ToListAsync();

        var dto = new PlanDto
        {
            Id = plan.Id,
            Year = plan.Year,
            Status = plan.Status,
            StatusTitle = StatusTitle(plan.Status),
            ApprovalProtocol = plan.ApprovalProtocol,
            ApprovedOn = plan.ApprovedOn,
            PlannedTotal = plan.Items.Sum(i => i.PlannedAmount),
        };

        var matchedRequestIds = new HashSet<int>();

        foreach (var item in plan.Items)
        {
            // Инициатор пишет ссылку свободным текстом («п. 4.2 Плана закупок на 2026 год»),
            // поэтому позиция ищется вхождением кода, а не точным равенством.
            var linked = requests
                .Where(r => r.PlanItem is not null && r.PlanItem.Contains(item.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var r in linked)
                matchedRequestIds.Add(r.Id);

            var actual = linked.Sum(r => r.Amount);

            dto.Items.Add(new PlanItemDto
            {
                Id = item.Id,
                Code = item.Code,
                Subject = item.Subject,
                PlannedAmount = item.PlannedAmount,
                Quarter = item.Quarter,
                OrgUnitTitle = item.OrgUnit?.TitleRu,
                SubjectKindTitle = SubjectKindTitle(item.SubjectKind),
                Note = item.Note,
                RequestCount = linked.Count,
                ActualAmount = actual,
                DeviationPercent = linked.Count == 0 || item.PlannedAmount <= 0
                    ? null
                    : Math.Round((actual - item.PlannedAmount) / item.PlannedAmount * 100m, 1),
                IsOverrun = actual > item.PlannedAmount,
            });
        }

        dto.ActualTotal = dto.Items.Sum(i => i.ActualAmount);

        var unplanned = requests.Where(r => !matchedRequestIds.Contains(r.Id)).ToList();
        dto.UnplannedRequestCount = unplanned.Count;
        dto.UnplannedAmount = unplanned.Sum(r => r.Amount);

        return dto;
    }

    private static string StatusTitle(PlanStatus status) => status switch
    {
        PlanStatus.Approved => "Утверждён",
        PlanStatus.Closed => "Год закрыт",
        _ => "Формируется",
    };

    private static string SubjectKindTitle(ProcurementSubjectKind kind) => kind switch
    {
        ProcurementSubjectKind.HouseholdGoods => "Хозяйственные товары",
        ProcurementSubjectKind.SpecificGoods => "Специфичный товар",
        ProcurementSubjectKind.GoodsWithInstallation => "Товар с установкой",
        ProcurementSubjectKind.Works => "Работы",
        ProcurementSubjectKind.Services => "Услуги",
        _ => "Товары",
    };
}
