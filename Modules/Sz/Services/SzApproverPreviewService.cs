using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Предпросмотр согласующих по виду записки: при выборе вида форма подставляет тех,
/// кто прописан в шаблоне маршрута этого вида (админ настраивает шаблон). Автор
/// может их поменять — это подсказка, а не жёсткий состав.
///
/// Тот же выбор шаблона и то же разрешение ролей, что и при реальном запуске
/// маршрута (SzService.InstantiateFromKindAsync), но без создания маршрута —
/// только список пользователей.
/// </summary>
public interface ISzApproverPreviewService
{
    Task<List<SzApproverPreviewDto>> PreviewAsync(
        int kindId, int? correspondentUnitId, CancellationToken ct = default);
}

public class SzApproverPreviewService : ISzApproverPreviewService
{
    private readonly DelosferaDbContext _db;
    private readonly IRouteTemplateSelector _templates;
    private readonly IRouteRoleResolver _roles;
    private readonly ICurrentUserService _currentUser;

    public SzApproverPreviewService(
        DelosferaDbContext db, IRouteTemplateSelector templates,
        IRouteRoleResolver roles, ICurrentUserService currentUser)
    {
        _db = db;
        _templates = templates;
        _roles = roles;
        _currentUser = currentUser;
    }

    public async Task<List<SzApproverPreviewDto>> PreviewAsync(
        int kindId, int? correspondentUnitId, CancellationToken ct = default)
    {
        // Автор — текущий пользователь; его подразделение задаёт адресный шаблон и
        // разрешение ролей author-head/author-curator.
        var authorUnitId = await _db.Users.AsNoTracking()
            .Where(u => u.Id == _currentUser.UserId)
            .Select(u => u.OrgUnitId)
            .FirstOrDefaultAsync(ct);

        // Приоритет источника маршрута повторяет запуск: адресный шаблон подразделения
        // или глобальный шаблон типа (SelectAsync), иначе — шаблон, привязанный к виду.
        var template = await _templates.SelectAsync(DocumentType.Sz, authorUnitId, ct);
        if (template is null)
        {
            var kindTemplateId = await _db.SzKinds.AsNoTracking()
                .Where(k => k.Id == kindId)
                .Select(k => k.RouteTemplateId)
                .FirstOrDefaultAsync(ct);

            if (kindTemplateId is int tid)
                template = await _db.RouteTemplates.AsNoTracking()
                    .Include(t => t.Steps).ThenInclude(s => s.Participants)
                    .FirstOrDefaultAsync(t => t.Id == tid, ct);
        }

        if (template is null) return [];

        var context = new RouteContext(0, authorUnitId, correspondentUnitId);

        // Порядок согласующих = порядок этапов шаблона. Одного человека в списке не
        // дублируем (мог оказаться в двух этапах разными ролями).
        var userIds = new List<int>();
        foreach (var step in template.Steps.OrderBy(s => s.Order))
        {
            foreach (var p in step.Participants)
            {
                var uid = p.UserId
                    ?? (p.UnitId is int unit
                            ? await _roles.ResolveAsync($"{RouteRoles.UnitHeadPrefix}{unit}", context, ct)
                            : p.RoleRef is not null
                                ? await _roles.ResolveAsync(p.RoleRef, context, ct)
                                : null);

                if (uid is int id && !userIds.Contains(id))
                    userIds.Add(id);
            }
        }

        if (userIds.Count == 0) return [];

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Position = u.Position != null ? u.Position.TitleRu : null,
            })
            .ToListAsync(ct);

        // Возвращаем в порядке шаблона.
        return userIds
            .Select(id => users.FirstOrDefault(x => x.Id == id))
            .Where(x => x is not null)
            .Select(x => new SzApproverPreviewDto
            {
                UserId = x!.Id,
                FullName = x.FullName,
                Position = x.Position,
            })
            .ToList();
    }
}
