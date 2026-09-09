using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Procurement.Services;

/// <summary>
/// Разово заводит глобальный шаблон маршрута закупки, повторяющий прежнюю
/// захардкоженную в ProcurementRouteService цепочку — на ролях (author-head,
/// unit-head:N и т.д.), с условными этапами (хоз.товары, вынесение на орган).
///
/// Идемпотентно: если шаблон уровня типа для закупки уже есть — не трогает.
/// Ссылки на подразделения (Админотдел, УПиА, Сектор закупок, Правление) резолвятся
/// по портальному ExternalId с откатом на название — как во FindUnitAsync.
/// </summary>
public static class ProcurementRouteTemplateSeeder
{
    // Те же ориентиры, что в ProcurementRouteService.
    private const int AdministrativeUnitPortalId = 55;
    private const string AdministrativeUnit = "Административный отдел";
    private const int BudgetUnitPortalId = 59;
    private const string BudgetUnit = "Управление стратегического планирования и бюджетирования";
    private const int ProcurementUnitPortalId = 49;
    private const string ProcurementUnit = "Сектор закупок";
    private const int StepTimeNormHours = 24;

    public static async Task SeedAsync(DelosferaDbContext db)
    {
        var exists = await db.RouteTemplates
            .AnyAsync(t => t.DocumentType == DocumentType.Procurement && t.OrgUnitId == null);
        if (exists) return;

        async Task<int?> UnitId(int portalId, string title)
        {
            var byPortal = await db.OrganizationUnits.FirstOrDefaultAsync(u => u.ExternalId == portalId);
            if (byPortal is not null) return byPortal.Id;
            return (await db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == title))?.Id;
        }

        var adminId = await UnitId(AdministrativeUnitPortalId, AdministrativeUnit);
        var budgetId = await UnitId(BudgetUnitPortalId, BudgetUnit);
        var procId = await UnitId(ProcurementUnitPortalId, ProcurementUnit);
        var boardId = (await db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == "Правление"))?.Id;

        // Обязательные для линейной цепочки подразделения должны существовать — иначе
        // шаблон получится неполным, и лучше оставить прежний код-путь как есть.
        if (budgetId is null || procId is null)
            return;

        var order = 0;
        var steps = new List<RouteTemplateStep>();

        void Step(string roleRef, StepKind kind = StepKind.Approval, string? condition = null)
        {
            steps.Add(new RouteTemplateStep
            {
                Order = ++order,
                Mode = StepMode.Sequential,
                Kind = kind,
                TimeNormHours = StepTimeNormHours,
                Condition = condition,
                Participants = new List<RouteTemplateParticipant>
                {
                    new() { RoleRef = roleRef, Required = true },
                },
            });
        }

        // Инициирующее подразделение заявки в контексте маршрута — это TargetUnit
        // (BuildContextAsync берёт ProcurementRequest.InitiatorUnitId), поэтому первые
        // два этапа — руководитель и куратор именно инициирующего подразделения.
        Step(RouteRoles.TargetUnitHead);
        Step(RouteRoles.TargetUnitCurator);
        if (adminId is { } a)
            Step($"{RouteRoles.UnitHeadPrefix}{a}", condition: ProcurementRouteConditions.HouseholdGoods);
        Step($"{RouteRoles.UnitHeadPrefix}{budgetId}");
        Step($"{RouteRoles.UnitCuratorPrefix}{procId}");
        Step($"{RouteRoles.UnitHeadPrefix}{procId}");
        if (boardId is { } b)
            Step($"{RouteRoles.UnitHeadPrefix}{b}", StepKind.Board, ProcurementRouteConditions.BoardAuthority);

        db.RouteTemplates.Add(new RouteTemplate
        {
            DocumentType = DocumentType.Procurement,
            Name = "Закупка — типовой маршрут",
            IsGlobalRule = true,
            OrgUnitId = null,
            Steps = steps,
        });
        await db.SaveChangesAsync();
    }
}
