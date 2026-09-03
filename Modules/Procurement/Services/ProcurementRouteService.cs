using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProcurementRouteService
{
    /// <summary>Построить и запустить маршрут согласования заявки (PRC-08).</summary>
    Task<RouteInstance> StartAsync(ProcurementRequest request, int actorUserId);
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
    private const string BudgetUnit = "Управление стратегического планирования и бюджетирования";

    /// <summary>
    /// Подразделение, которое ведёт процедуру закупки.
    ///
    /// Раньше два последних этапа брали людей из Административного отдела, хотя
    /// назывались «Сектор закупок»: заявка уходила не туда, а сам сектор её не
    /// видел. Названия этапов при этом читались правильно, и по карточке подмена
    /// не замечалась.
    /// </summary>
    private const string ProcurementUnit = "Сектор закупок";

    private readonly DelosferaDbContext _db;
    private readonly IRouteEngine _engine;

    public ProcurementRouteService(DelosferaDbContext db, IRouteEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<RouteInstance> StartAsync(ProcurementRequest request, int actorUserId)
    {
        var initiatorUnit = request.InitiatorUnitId is { } unitId
            ? await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.Id == unitId)
            : null;

        if (initiatorUnit is null)
            throw new InvalidOperationException("Не указано инициирующее подразделение — маршрут не построить");

        var adminUnit = await FindUnitAsync(AdministrativeUnit);
        var budgetUnit = await FindUnitAsync(BudgetUnit);
        var procurementUnit = await FindUnitAsync(ProcurementUnit);

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

        await _engine.StartAsync(instance.Id, actorUserId);
        return instance;
    }

    private async Task<OrganizationUnit?> FindUnitAsync(string titleRu) =>
        await _db.OrganizationUnits.FirstOrDefaultAsync(u => u.TitleRu == titleRu);

    private static string AuthorityTitle(ApprovalAuthority authority) => authority switch
    {
        ApprovalAuthority.SupervisoryBoard => "Совет директоров",
        ApprovalAuthority.Shareholders => "Общее собрание акционеров",
        _ => "Правление",
    };
}
