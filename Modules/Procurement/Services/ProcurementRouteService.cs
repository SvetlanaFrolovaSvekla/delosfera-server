using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProcurementRouteService
{
    /// <summary>
    /// Построить и запустить маршрут согласования заявки (PRC-08).
    /// extraApproverUserIds — дополнительные согласующие, которых инициатор добавил
    /// сверх автоподобранной цепочки; идут отдельными этапами в конце.
    /// </summary>
    Task<RouteInstance> StartAsync(
        ProcurementRequest request, int actorUserId, IReadOnlyList<int>? extraApproverUserIds = null);
}

/// <summary>
/// Маршрут согласования заявки на закупку (PRC-08):
/// инициатор → руководитель СП → куратор СП → [Административный отдел — для хозтоваров]
/// → УПиА (бюджетный контроль) → куратор Сектора закупок → Сектор закупок.
///
/// Маршрут собирается под конкретную заявку, а не копируется из шаблона: состав
/// зависит от инициирующего подразделения, типа предмета и решения Матрицы полномочий,
/// а шаблон хранит фиксированных участников и такое ветвление не выражает.
/// </summary>
public class ProcurementRouteService : IProcurementRouteService
{
    /// <summary>Норматив на этап согласования заявки, часов.</summary>
    private const int StepTimeNormHours = 24;

    private const string AdministrativeUnit = "Административный отдел";

    /// <summary>Номер Административного отдела в портале банка.</summary>
    private const int AdministrativeUnitPortalId = 55;

    private const string BudgetUnit = "Управление стратегического планирования и бюджетирования";

    /// <summary>
    /// Номер бюджетного управления в портале. В портале оно называется
    /// «Управление планирования и анализа», у нас в сиде — иначе, и по названию
    /// находилась пустая запись вместо управления с четырьмя сотрудниками.
    /// </summary>
    private const int BudgetUnitPortalId = 59;

    /// <summary>
    /// Подразделение, которое ведёт процедуру закупки.
    ///
    /// Раньше два последних этапа брали людей из Административного отдела, хотя
    /// назывались «Сектор закупок»: заявка уходила не туда, а сам сектор её не
    /// видел. Названия этапов при этом читались правильно, и по карточке подмена
    /// не замечалась.
    /// </summary>
    private const string ProcurementUnit = "Сектор закупок";

    /// <summary>Номер Сектора закупок в портале банка.</summary>
    private const int ProcurementUnitPortalId = 49;

    private readonly DelosferaDbContext _db;
    private readonly IRouteEngine _engine;
    private readonly IRouteTemplateSelector _templates;

    public ProcurementRouteService(DelosferaDbContext db, IRouteEngine engine, IRouteTemplateSelector templates)
    {
        _db = db;
        _engine = engine;
        _templates = templates;
    }

    public async Task<RouteInstance> StartAsync(
        ProcurementRequest request, int actorUserId, IReadOnlyList<int>? extraApproverUserIds = null)
    {
        // Единый конструктор: если для закупки настроен шаблон (уровня типа или под
        // инициирующее подразделение) — маршрут строится из него. Условные этапы
        // (хозтовары, вынесение на орган) включаются по вычисленным здесь условиям.
        // Иначе — прежняя программная сборка ниже (защита на случай отсутствия шаблона).
        var template = await _templates.SelectAsync(
            Documents.Models.DocumentType.Procurement, request.InitiatorUnitId);
        if (template is not null)
        {
            var conditions = new HashSet<string>();
            if (request.SubjectKind == ProcurementSubjectKind.HouseholdGoods)
                conditions.Add(ProcurementRouteConditions.HouseholdGoods);
            if (request.ApprovalAuthority is ApprovalAuthority.Board
                or ApprovalAuthority.SupervisoryBoard
                or ApprovalAuthority.Shareholders)
                conditions.Add(ProcurementRouteConditions.BoardAuthority);

            var fromTemplate = await _engine.InstantiateFromTemplateAsync(
                request.DocumentId, template.Id, conditions);
            await AppendExtraApproversAsync(fromTemplate, extraApproverUserIds);
            await _engine.StartAsync(fromTemplate.Id, actorUserId);
            return fromTemplate;
        }

        var initiatorUnit = request.InitiatorUnitId is { } unitId
            ? await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.Id == unitId)
            : null;

        if (initiatorUnit is null)
            throw new InvalidOperationException("Не указано инициирующее подразделение — маршрут не построить");

        var adminUnit = await FindUnitAsync(AdministrativeUnit, AdministrativeUnitPortalId);
        var budgetUnit = await FindUnitAsync(BudgetUnit, BudgetUnitPortalId);
        var procurementUnit = await FindUnitAsync(ProcurementUnit, ProcurementUnitPortalId);

        var steps = new List<(string Title, int? UserId, StepKind Kind)>();

        // Руководитель и куратор инициирующего СП — первые два визирующих.
        steps.Add(("Руководитель инициирующего подразделения", initiatorUnit.HeadUserId, StepKind.Approval));
        steps.Add(("Куратор инициирующего подразделения", initiatorUnit.CuratorUserId, StepKind.Approval));

        // Хозяйственные товары дополнительно проверяет Административный отдел.
        if (request.SubjectKind == ProcurementSubjectKind.HouseholdGoods)
            steps.Add(("Административный отдел (хозяйственные товары)", adminUnit?.HeadUserId, StepKind.Approval));

        // Бюджетный контроль: виза УПиА о наличии средств.
        steps.Add(("УПиА — бюджетный контроль", budgetUnit?.HeadUserId, StepKind.Approval));

        // Куратор Сектора закупок и сам сектор, который проводит процедуру.
        steps.Add(("Куратор Сектора закупок", procurementUnit?.CuratorUserId, StepKind.Approval));
        steps.Add(("Сектор закупок — проведение процедуры", procurementUnit?.HeadUserId, StepKind.Approval));

        // Если расход утверждает коллегиальный орган — отдельный этап с загрузкой
        // выписки из протокола (PRC-06). Решение принимает не один человек, поэтому
        // задача ставится подписанту Правления.
        if (request.ApprovalAuthority is ApprovalAuthority.Board
            or ApprovalAuthority.SupervisoryBoard
            or ApprovalAuthority.Shareholders)
        {
            var boardUnit = await FindUnitAsync("Правление");
            steps.Add(($"Вынесение на {AuthorityTitle(request.ApprovalAuthority)}", boardUnit?.HeadUserId, StepKind.Board));
        }

        // Этап без назначенного сотрудника означал бы согласующего, которому некому
        // поставить задачу: маршрут встал бы молча. Лучше отказать сразу и назвать,
        // кого не хватает в оргструктуре.
        var unresolved = steps.Where(s => s.UserId is null).Select(s => s.Title).ToList();
        if (unresolved.Count > 0)
            throw new InvalidOperationException(
                "Не удалось определить согласующих: " + string.Join("; ", unresolved) +
                ". Заполните руководителя и куратора подразделения в справочнике оргструктуры");

        var order = 1;
        var instance = new RouteInstance
        {
            DocumentId = request.DocumentId,
            Status = RouteInstanceStatus.Draft,
            Steps = steps.Select(s => new RouteStep
            {
                Order = order++,
                Mode = StepMode.Sequential,
                Kind = s.Kind,
                TimeNormHours = StepTimeNormHours,
                Participants =
                [
                    new RouteParticipant
                    {
                        UserId = s.UserId,
                        Required = true,
                        State = ParticipantState.Pending,
                    },
                ],
            }).ToList(),
        };

        _db.RouteInstances.Add(instance);
        await _db.SaveChangesAsync();

        await AppendExtraApproversAsync(instance, extraApproverUserIds);
        await _engine.StartAsync(instance.Id, actorUserId);
        return instance;
    }

    /// <summary>
    /// Дополнительные согласующие инициатора: каждый — отдельным этапом в конце
    /// маршрута. Пропускаем несуществующих и уже присутствующих в маршруте, чтобы не
    /// задвоить. Обязательный (автоподобранный) состав не трогаем — только добавляем.
    /// </summary>
    private async Task AppendExtraApproversAsync(RouteInstance instance, IReadOnlyList<int>? extraApproverUserIds)
    {
        if (extraApproverUserIds is null || extraApproverUserIds.Count == 0) return;

        var already = instance.Steps
            .SelectMany(s => s.Participants)
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!.Value)
            .ToHashSet();

        var wanted = extraApproverUserIds.Distinct().Where(id => !already.Contains(id)).ToList();
        if (wanted.Count == 0) return;

        var valid = await _db.Users.Where(u => wanted.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        var order = instance.Steps.Count == 0 ? 1 : instance.Steps.Max(s => s.Order) + 1;

        foreach (var userId in valid)
        {
            instance.Steps.Add(new RouteStep
            {
                Order = order++,
                Mode = StepMode.Sequential,
                Kind = StepKind.Approval,
                TimeNormHours = StepTimeNormHours,
                Participants =
                [
                    new RouteParticipant { UserId = userId, Required = true, State = ParticipantState.Pending },
                ],
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Подразделение маршрута: сначала по номеру портала, потом по названию.
    ///
    /// Названия расходятся, и это уже подвело: бюджетный контроль искался как
    /// «Управление стратегического планирования и бюджетирования» — так называется
    /// наша пустая запись из сида, а настоящее управление приходит из портала под
    /// именем «Управление планирования и анализа». Этап уходил в подразделение без
    /// единого сотрудника, и заявка вставала.
    ///
    /// Номер портала у записи не меняется, куда бы её ни перенесли внутри нашего
    /// справочника, поэтому ищем сперва по нему.
    /// </summary>
    private async Task<OrganizationUnit?> FindUnitAsync(string titleRu, int? portalId = null)
    {
        if (portalId is { } id)
        {
            var byPortal = await _db.OrganizationUnits
                .FirstOrDefaultAsync(u => u.ExternalId == id);

            if (byPortal is not null) return byPortal;
        }

        return await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == titleRu);
    }

    private static string AuthorityTitle(ApprovalAuthority authority) => authority switch
    {
        ApprovalAuthority.SupervisoryBoard => "Совет директоров",
        ApprovalAuthority.Shareholders => "Общее собрание акционеров",
        _ => "Правление",
    };
}
