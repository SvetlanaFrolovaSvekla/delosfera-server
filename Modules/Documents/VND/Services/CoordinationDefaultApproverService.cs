using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class CoordinationDefaultApproverService : ICoordinationDefaultApproverService
{
    private readonly DelosferaDbContext _db;

    private readonly IFixedApprovalUnitResolver _fixedUnits;

    public CoordinationDefaultApproverService(
        DelosferaDbContext db, IFixedApprovalUnitResolver fixedUnits)
    {
        _db = db;
        _fixedUnits = fixedUnits;
    }

    public async Task<List<CoordinationDefaultApproverResponse>> GetAllAsync()
    {
        var entities = await _db.Set<CoordinationDefaultApprover>()
            .Include(x => x.ApproverUser)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var unitByKind = new Dictionary<ApprovalStageKind, int>();
        foreach (var kind in entities.Select(x => x.Kind).Distinct())
            unitByKind[kind] = await ExpectedOrgUnitIdAsync(kind);

        var orgUnitIds = unitByKind.Values.Distinct().ToList();
        var orgUnits = await _db.OrganizationUnits
            .Where(x => orgUnitIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.TitleRu);

        return entities.Select(x => ToResponse(x, unitByKind[x.Kind], orgUnits)).ToList();
    }

    public async Task<CoordinationDefaultApproverResponse> UpdateAsync(
        int id, UpdateCoordinationDefaultApproverRequest request)
    {
        var entity = await _db.Set<CoordinationDefaultApprover>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Запись справочника обязательных участников с id={id} не найдена");

        if (request.ApproverUserId.HasValue)
        {
            var approver = await _db.Users.FindAsync(request.ApproverUserId.Value)
                ?? throw new KeyNotFoundException($"Пользователь с id={request.ApproverUserId} не найден");

            var expectedOrgUnitId = await ExpectedOrgUnitIdAsync(entity.Kind);
            if (approver.OrgUnitId != expectedOrgUnitId)
                throw new InvalidOperationException(
                    $"Согласующий по умолчанию для этапа «{KindTitle(entity.Kind)}» должен относиться " +
                    "к соответствующему подразделению");
        }

        entity.ApproverUserId = request.ApproverUserId;
        await _db.SaveChangesAsync();

        var reloaded = await _db.Set<CoordinationDefaultApprover>()
            .Include(x => x.ApproverUser)
            .FirstAsync(x => x.Id == id);

        var orgUnitId = await ExpectedOrgUnitIdAsync(reloaded.Kind);
        var orgUnitTitle = await _db.OrganizationUnits
            .Where(x => x.Id == orgUnitId)
            .Select(x => x.TitleRu)
            .FirstOrDefaultAsync() ?? "";

        return ToResponse(reloaded, orgUnitId, new Dictionary<int, string> { [orgUnitId] = orgUnitTitle });
    }

    /// <summary>Подразделение, обязательное для согласующего данного фиксированного этапа —
    /// та же логика, что и в VndApprovalService.BuildAndValidateStagesAsync</summary>
    private async Task<int> ExpectedOrgUnitIdAsync(ApprovalStageKind kind) =>
        await _fixedUnits.ResolveAsync(kind)
        ?? throw new InvalidOperationException(
            $"Этап {kind} не является фиксированным или его подразделение не заведено в справочнике");

    private static string KindTitle(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "Юридическое управление",
        ApprovalStageKind.RiskManagement => "Риск-менеджмент",
        ApprovalStageKind.Compliance => "Комплаенс-контроль",
        ApprovalStageKind.Methodology => "Методология",
        _ => kind.ToString()
    };

    private static string MapKind(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "legal",
        ApprovalStageKind.RiskManagement => "risk_management",
        ApprovalStageKind.Compliance => "compliance",
        ApprovalStageKind.Methodology => "methodology",
        _ => "custom"
    };

    private static CoordinationDefaultApproverResponse ToResponse(
        CoordinationDefaultApprover entity, int orgUnitId, Dictionary<int, string> orgUnitTitles)
    {
        return new CoordinationDefaultApproverResponse
        {
            Id = entity.Id,
            Kind = MapKind(entity.Kind),
            KindTitle = KindTitle(entity.Kind),
            OrgUnitId = orgUnitId,
            OrgUnitName = orgUnitTitles.GetValueOrDefault(orgUnitId, ""),
            ApproverUserId = entity.ApproverUserId,
            ApproverName = entity.ApproverUser?.FullName,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}