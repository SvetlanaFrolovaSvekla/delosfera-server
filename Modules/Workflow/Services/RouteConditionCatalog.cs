using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Procurement.Services;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Условия включения этапов маршрута, доступные в конструкторе для данного вида
/// документа. Список задаётся на сервере, чтобы конструктор не хардкодил коды и не
/// расходился с контуром, который эти условия вычисляет и передаёт при запуске.
///
/// Условия привязаны к виду документа: у закупки свои (хозтовары, порог полномочий),
/// у СЗ и ВНД условных этапов пока нет — маршрут у них безусловный.
/// </summary>
public static class RouteConditionCatalog
{
    public static IEnumerable<(string Value, string Title)> For(DocumentType type) => type switch
    {
        DocumentType.Procurement =>
        [
            (ProcurementRouteConditions.HouseholdGoods, "Хозтовары — доп. виза Административного отдела"),
            (ProcurementRouteConditions.BoardAuthority, "Порог полномочий — вынесение на орган"),
        ],
        _ => [],
    };
}
