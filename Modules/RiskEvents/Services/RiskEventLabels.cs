using delosfera_server.Modules.RiskEvents.Models;

namespace delosfera_server.Modules.RiskEvents.Services;

/// <summary>
/// Русские формулировки типов, серьёзности и статусов событий операционного риска
/// (RISK-1). Формулировки типов — из классификации 716-П. Выведены в один класс,
/// чтобы реестр и карточка не расходились в подписях.
/// </summary>
public static class RiskEventLabels
{
    public static string Type(RiskEventType type) => type switch
    {
        RiskEventType.InternalFraud => "Внутреннее мошенничество",
        RiskEventType.ExternalFraud => "Внешнее мошенничество",
        RiskEventType.EmploymentPractices => "Кадровая политика и безопасность труда",
        RiskEventType.ClientsProductsBusinessPractices => "Клиенты, продукты и деловая практика",
        RiskEventType.DamageToPhysicalAssets => "Ущерб материальным активам",
        RiskEventType.BusinessDisruptionSystemFailures => "Нарушение деятельности и сбои систем",
        RiskEventType.ExecutionDeliveryProcessManagement => "Управление процессами, исполнение и поставка",
        _ => type.ToString(),
    };

    public static string Severity(RiskEventSeverity severity) => severity switch
    {
        RiskEventSeverity.Low => "Незначительная",
        RiskEventSeverity.Medium => "Умеренная",
        RiskEventSeverity.High => "Существенная",
        RiskEventSeverity.Critical => "Критическая",
        _ => severity.ToString(),
    };

    public static string Status(RiskEventStatus status) => status switch
    {
        RiskEventStatus.New => "Новое",
        RiskEventStatus.InProgress => "В работе",
        RiskEventStatus.Closed => "Закрыто",
        _ => status.ToString(),
    };
}
