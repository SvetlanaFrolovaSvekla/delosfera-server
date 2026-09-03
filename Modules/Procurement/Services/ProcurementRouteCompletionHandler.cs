using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Procurement.Services;

/// <summary>
/// Статус заявки на закупку следует за её маршрутом согласования.
///
/// Обработчика у этого контура не было вовсе: заявка проходила все визы —
/// руководителя, куратора, бюджетный контроль, Сектор закупок — и навсегда
/// оставалась «на согласовании». Статусы «согласована», «в закупке» и
/// «завершена» в коде только читались счётчиками и на панели показателей;
/// выставить их было некому, поэтому там всегда стояли нули.
/// </summary>
public class ProcurementRouteCompletionHandler : IRouteCompletionHandler
{
    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;

    public ProcurementRouteCompletionHandler(
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
        // Маршруты других контуров сюда не относятся — сверяемся с типом документа.
        var request = await _db.ProcurementRequests
            .Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.DocumentId == documentId
                                      && r.Document!.Type == DocumentType.Procurement);

        if (request is null) return;

        var status = routeStatus switch
        {
            // Согласование пройдено — заявка переходит к процедуре: последним её
            // визирует Сектор закупок, и дальше он же её и проводит.
            RouteInstanceStatus.Approved => ProcurementStatus.InProcurement,

            RouteInstanceStatus.Rejected => ProcurementStatus.Rejected,
            RouteInstanceStatus.OnRevision => ProcurementStatus.OnRevision,
            RouteInstanceStatus.Running => ProcurementStatus.OnApproval,
            _ => null,
        };

        if (status is null || request.Document!.StatusCode == status) return;

        await _documents.ChangeStatusAsync(documentId, status, actorUserId);
        await _audit.LogAsync("ProcurementRequest", request.Id, "StatusFromRoute", actorUserId,
            new {route = routeStatus.ToString(), status});
    }
}
