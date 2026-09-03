using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class CoordinationDefaultApproverService : ICoordinationDefaultApproverService
{
    private readonly DelosferaDbContext _db;

    public CoordinationDefaultApproverService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<CoordinationDefaultApproverResponse>> GetAllAsync()
    {
        var entities = await _db.Set<CoordinationDefaultApprover>()
            .Include(x => x.OrgUnit)
            .Include(x => x.ApproverUser)
            .OrderBy(x => x.Order)
            .ToListAsync();

        return entities.Select(ToResponse).ToList();
    }

    public async Task<CoordinationDefaultApproverResponse> CreateAsync(CreateCoordinationDefaultApproverRequest request)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
            throw new InvalidOperationException("Название этапа не может быть пустым");

        var orgUnit = await _db.OrganizationUnits.FindAsync(request.OrgUnitId)
            ?? throw new KeyNotFoundException($"Подразделение с id={request.OrgUnitId} не найдено");

        if (request.ApproverUserId.HasValue)
            await ValidateApproverAsync(request.ApproverUserId.Value, request.OrgUnitId);

        var maxOrder = await _db.Set<CoordinationDefaultApprover>()
            .Select(x => (int?)x.Order)
            .MaxAsync() ?? 0;

        var now = DateTime.UtcNow;
        var entity = new CoordinationDefaultApprover
        {
            Title = title,
            OrgUnitId = orgUnit.Id,
            Order = maxOrder + 1,
            ApproverUserId = request.ApproverUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Set<CoordinationDefaultApprover>().Add(entity);
        await _db.SaveChangesAsync();

        return await LoadResponseAsync(entity.Id);
    }

    public async Task<CoordinationDefaultApproverResponse> UpdateAsync(
        int id, UpdateCoordinationDefaultApproverRequest request)
    {
        var entity = await _db.Set<CoordinationDefaultApprover>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Запись справочника обязательных этапов с id={id} не найдена");

        var title = request.Title.Trim();
        if (title.Length == 0)
            throw new InvalidOperationException("Название этапа не может быть пустым");

        var orgUnit = await _db.OrganizationUnits.FindAsync(request.OrgUnitId)
            ?? throw new KeyNotFoundException($"Подразделение с id={request.OrgUnitId} не найдено");

        if (request.ApproverUserId.HasValue)
            await ValidateApproverAsync(request.ApproverUserId.Value, request.OrgUnitId);

        entity.Title = title;
        entity.OrgUnitId = orgUnit.Id;
        entity.ApproverUserId = request.ApproverUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await LoadResponseAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Set<CoordinationDefaultApprover>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Запись справочника обязательных этапов с id={id} не найдена");

        _db.Set<CoordinationDefaultApprover>().Remove(entity);

        // Уже запущенные маршруты не зависят от этой записи (см. VndApprovalStage.Title/
        // OrgUnitId/ApproverUserId - снимок при построении, CoordinationStageId - SetNull),
        // поэтому удаление ничего в истории согласования не ломает.
        await _db.SaveChangesAsync();

        await RenumberAsync();
    }

    public async Task<List<CoordinationDefaultApproverResponse>> ReorderAsync(
        ReorderCoordinationDefaultApproverRequest request)
    {
        var entities = await _db.Set<CoordinationDefaultApprover>().ToListAsync();

        var currentIds = entities.Select(x => x.Id).ToHashSet();
        var requestedIds = request.OrderedIds;

        if (requestedIds.Count != currentIds.Count || requestedIds.Distinct().Count() != requestedIds.Count
            || !requestedIds.All(currentIds.Contains))
            throw new InvalidOperationException(
                "Новый порядок должен содержать все существующие записи справочника, каждую ровно один раз");

        var byId = entities.ToDictionary(x => x.Id);
        for (var i = 0; i < requestedIds.Count; i++)
        {
            byId[requestedIds[i]].Order = i + 1;
            byId[requestedIds[i]].UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return await GetAllAsync();
    }

    /// <summary>Согласующий по умолчанию должен реально существовать и относиться к указанному
    /// подразделению - та же проверка, что и при построении маршрута
    /// (VndApprovalService.BuildAndValidateStagesAsync).</summary>
    private async Task ValidateApproverAsync(int approverUserId, int orgUnitId)
    {
        var approver = await _db.Users.FindAsync(approverUserId)
            ?? throw new KeyNotFoundException($"Пользователь с id={approverUserId} не найден");

        if (approver.OrgUnitId != orgUnitId)
            throw new InvalidOperationException(
                "Согласующий по умолчанию для этапа должен относиться к указанному подразделению");
    }

    /// <summary>Восстанавливает сплошную нумерацию Order (1..N без пропусков) после удаления
    /// записи - Order уникален и на нём завязан порядок этапов в маршруте.</summary>
    private async Task RenumberAsync()
    {
        var remaining = await _db.Set<CoordinationDefaultApprover>()
            .OrderBy(x => x.Order)
            .ToListAsync();

        var changed = false;
        for (var i = 0; i < remaining.Count; i++)
        {
            var expectedOrder = i + 1;
            if (remaining[i].Order == expectedOrder) continue;
            remaining[i].Order = expectedOrder;
            remaining[i].UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed) await _db.SaveChangesAsync();
    }

    private async Task<CoordinationDefaultApproverResponse> LoadResponseAsync(int id)
    {
        var entity = await _db.Set<CoordinationDefaultApprover>()
            .Include(x => x.OrgUnit)
            .Include(x => x.ApproverUser)
            .FirstAsync(x => x.Id == id);

        return ToResponse(entity);
    }

    private static CoordinationDefaultApproverResponse ToResponse(CoordinationDefaultApprover entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Order = entity.Order,
        OrgUnitId = entity.OrgUnitId,
        OrgUnitName = entity.OrgUnit?.TitleRu ?? "",
        ApproverUserId = entity.ApproverUserId,
        ApproverName = entity.ApproverUser?.FullName,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
