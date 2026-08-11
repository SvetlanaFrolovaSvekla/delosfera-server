using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Реакция контура СЗ на события маршрута: статус записки ведётся за маршрутом
/// (согласован → на исполнении, отклонён → забракована, замечания → на доработке).
///
/// Обработчик намеренно не зависит от ISzService: движок сам получает обработчики,
/// а SzService зависит от движка — через сервис контура получилось бы кольцо в DI.
/// </summary>
public class SzRouteCompletionHandler : IRouteCompletionHandler
{
    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;

    public SzRouteCompletionHandler(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
    }

    public Task OnRouteApprovedAsync(int routeInstanceId, int documentId, int actorUserId) =>
        SyncAsync(documentId, RouteInstanceStatus.Approved, actorUserId);

    public Task OnRouteStatusChangedAsync(
        int routeInstanceId, int documentId, RouteInstanceStatus status, int actorUserId) =>
        SyncAsync(documentId, status, actorUserId);

    private async Task SyncAsync(int documentId, RouteInstanceStatus routeStatus, int actorUserId)
    {
        // Статус записки ведётся за маршрутом: иначе документ оставался бы «Зарегистрирован»,
        // пока согласующие уже вернули его на доработку.
        var status = routeStatus switch
        {
            RouteInstanceStatus.Approved => SzStatus.OnExecution,
            RouteInstanceStatus.Rejected => SzStatus.Rejected,
            RouteInstanceStatus.OnRevision => SzStatus.OnRevision,
            RouteInstanceStatus.Running => SzStatus.Registered,
            _ => null
        };
        if (status is null) return;

        // Маршруты других контуров сюда не относятся — сверяемся с типом документа.
        var sz = await _db.SzDocuments
            .Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.Document!.Type == DocumentType.Sz);

        if (sz is null || sz.Document!.StatusCode == status) return;

        await _documents.ChangeStatusAsync(documentId, status, actorUserId);
        await _audit.LogAsync("Sz", sz.Id, "StatusFromRoute", actorUserId,
            new { route = routeStatus.ToString(), status });
    }
}
