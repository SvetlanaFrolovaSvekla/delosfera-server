namespace delosfera_server.Modules.Procurement.Services;

/// <summary>
/// Условия включения этапов маршрута закупки в шаблоне (RouteTemplateStep.Condition).
/// Контур вычисляет набор выполненных условий по заявке и передаёт его при
/// инстанцировании маршрута; движок включает условный этап только если его условие
/// в наборе.
/// </summary>
public static class ProcurementRouteConditions
{
    /// <summary>Хозяйственные товары — дополнительная виза Административного отдела.</summary>
    public const string HouseholdGoods = "household-goods";

    /// <summary>Порог полномочий требует вынесения на Правление/СД/ОСА.</summary>
    public const string BoardAuthority = "board-authority";
}
