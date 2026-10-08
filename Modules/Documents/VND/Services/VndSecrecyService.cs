using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndSecrecyService
{
    Task<VndSecrecyResponse> GetAsync(int vndId);
    Task<VndSecrecyResponse> UpdateAsync(int vndId, UpdateVndSecrecyRequest request, int currentUserId);
}

/// <summary>Настройки секретности ВНД: признак и кому открыт документ (группы + отдельные люди).
/// Доступ к самим эндпоинтам — по праву ManageVndSecrecy (см. VndSecrecyController).</summary>
public class VndSecrecyService : IVndSecrecyService
{
    private readonly DelosferaDbContext _db;
    private readonly IActivityLogService _activityLog;

    public VndSecrecyService(DelosferaDbContext db, IActivityLogService activityLog)
    {
        _db = db;
        _activityLog = activityLog;
    }

    public async Task<VndSecrecyResponse> GetAsync(int vndId)
    {
        var vnd = await _db.VndDocuments.AsNoTracking()
            .Include(x => x.SecretAccessGroups)
            .Include(x => x.SecretAccessUsers).ThenInclude(u => u.Position)
            .Include(x => x.SecretAccessUsers).ThenInclude(u => u.OrgUnit)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == vndId)
            ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        return new VndSecrecyResponse
        {
            IsSecret = vnd.IsSecret,
            GroupIds = vnd.SecretAccessGroups.Select(g => g.Id).OrderBy(i => i).ToList(),
            Users = vnd.SecretAccessUsers
                .OrderBy(u => u.FullName)
                .Select(u => new VndSecrecyUserResponse
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    OrgUnitName = u.OrgUnit?.TitleRu,
                    PositionName = u.Position?.TitleRu,
                    IsInactive = !u.IsActive || u.BlockedAt != null,
                })
                .ToList(),
        };
    }

    public async Task<VndSecrecyResponse> UpdateAsync(int vndId, UpdateVndSecrecyRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments
            .Include(x => x.SecretAccessGroups)
            .Include(x => x.SecretAccessUsers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == vndId)
            ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var groupIds = request.GroupIds.Distinct().ToList();
        var userIds = request.UserIds.Distinct().ToList();

        var groups = await _db.UserGroups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
        if (groups.Count != groupIds.Count)
            throw new InvalidOperationException("Одна из выбранных групп пользователей не найдена");

        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
        if (users.Count != userIds.Count)
            throw new InvalidOperationException("Один из выбранных пользователей не найден");

        // Секретный документ без единого допущенного пользователя бывает осознанным (его видят
        // только администратор и главный редактор), поэтому пустые списки не запрещаем.
        var wasSecret = vnd.IsSecret;

        vnd.IsSecret = request.IsSecret;
        vnd.SecretAccessGroups.Clear();
        foreach (var g in groups) vnd.SecretAccessGroups.Add(g);
        vnd.SecretAccessUsers.Clear();
        foreach (var u in users) vnd.SecretAccessUsers.Add(u);

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        var (ru, en, kg) = (wasSecret, request.IsSecret) switch
        {
            (false, true) => (
                $"{actorName} сделал(а) ВНД {vnd.Code} секретным (групп: {groups.Count}, пользователей: {users.Count})",
                $"{actorName} marked VND {vnd.Code} as secret (groups: {groups.Count}, users: {users.Count})",
                $"{actorName} {vnd.Code} ВНДсин купуя кылды (топтор: {groups.Count}, колдонуучулар: {users.Count})"),
            (true, false) => (
                $"{actorName} снял(а) секретность с ВНД {vnd.Code}",
                $"{actorName} removed secrecy from VND {vnd.Code}",
                $"{actorName} {vnd.Code} ВНДсинен купуялуулукту алып салды"),
            _ => (
                $"{actorName} изменил(а) настройки секретности ВНД {vnd.Code} (групп: {groups.Count}, пользователей: {users.Count})",
                $"{actorName} changed secrecy settings of VND {vnd.Code} (groups: {groups.Count}, users: {users.Count})",
                $"{actorName} {vnd.Code} ВНДсинин купуялуулук жөндөөлөрүн өзгөрттү"),
        };

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Other, vnd.Id, vnd.Code, currentUserId,
            new ActivityText(ru, en, kg),
            "/base-vnd");

        await _db.SaveChangesAsync();
        return await GetAsync(vndId);
    }
}
