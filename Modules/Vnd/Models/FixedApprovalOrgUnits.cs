namespace delosfera_server.Modules.Vnd.Models;

/// <summary>
/// Id подразделений для фиксированных этапов маршрута согласования.
/// Пока что СП в dictionary_organization_unit
/// TODO: сделать функцию для главного редактора - добавление фиксированных пользователей/СП во все маршруты
/// </summary>
public static class FixedApprovalOrgUnits
{
    public const int LegalOrgUnitId = 34;          // Юридическое управление
    public const int RiskManagementOrgUnitId = 28;  // Управление риск-менеджмента
    public const int ComplianceOrgUnitId = 5;        // Управление комплаенс контроля
    public const int MethodologyOrgUnitId = 33;      // Управление методологии и продуктов
}