using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Единый выбор шаблона маршрута по (тип документа + подразделение-инициатор).
/// Заменяет захардкоженные в коде цепочки согласующих: администратор настраивает
/// шаблон на тип, а при необходимости — отдельный для конкретного подразделения.
/// </summary>
public interface IRouteTemplateSelector
{
    /// <summary>
    /// Шаблон для типа и подразделения: сперва точное совпадение по подразделению,
    /// иначе шаблон уровня типа (OrgUnitId = null). null — подходящего шаблона нет.
    /// Возвращает шаблон со Steps/Participants, готовый к инстанцированию.
    /// </summary>
    Task<RouteTemplate?> SelectAsync(DocumentType type, int? orgUnitId, CancellationToken ct = default);
}

public class RouteTemplateSelector : IRouteTemplateSelector
{
    private readonly DelosferaDbContext _db;

    public RouteTemplateSelector(DelosferaDbContext db) => _db = db;

    public async Task<RouteTemplate?> SelectAsync(DocumentType type, int? orgUnitId, CancellationToken ct = default)
    {
        var candidates = await _db.Set<RouteTemplate>()
            .Include(t => t.Steps).ThenInclude(s => s.Participants)
            .Where(t => t.DocumentType == type && (t.OrgUnitId == null || t.OrgUnitId == orgUnitId))
            .ToListAsync(ct);

        // Шаблон, привязанный к подразделению, важнее шаблона уровня типа.
        return candidates
            .OrderByDescending(t => t.OrgUnitId == orgUnitId && orgUnitId != null)
            .ThenByDescending(t => t.IsGlobalRule)
            .FirstOrDefault();
    }
}
