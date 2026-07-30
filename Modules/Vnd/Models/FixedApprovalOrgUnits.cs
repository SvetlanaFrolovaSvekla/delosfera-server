namespace delosfera_server.Modules.Vnd.Models;

/// <summary>
/// Id подразделений для фиксированных этапов маршрута согласования.
/// Пока что реальные СП в dictionary_organization_unit.
/// </summary>
public static class FixedApprovalOrgUnits
{
    public const int LegalOrgUnitId = 34;          // Юридическое управление
    public const int RiskManagementOrgUnitId = 28;  // Управление риск-менеджмента
    public const int ComplianceOrgUnitId = 5;        // Управление комплаенс контроля
    public const int MethodologyOrgUnitId = 33;      // Управление методологии и продуктов
}