namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// УСТАРЕЛО и больше не используется. Раньше здесь хранились id 4 захардкоженных подразделений
/// для фиксированных этапов - теперь СП каждого обязательного этапа хранится прямо в записи
/// справочника CoordinationDefaultApprover.OrgUnitId (dictionaries/coordination-users) и
/// редактируется через CRUD. Класс оставлен только чтобы не ломать возможные внешние ссылки/
/// историю git - можно безопасно удалить отдельным коммитом после проверки, что нигде больше
/// не используется. Значения ниже также используются как запасные (Local) в
/// FixedApprovalUnitResolver - см. его комментарий про баг с дублем «Управление методологии»/
/// «Отдел методологии».
/// </summary>
[Obsolete("Заменено динамическим справочником CoordinationDefaultApprover.OrgUnitId")]
public static class FixedApprovalOrgUnits
{
    public const int LegalOrgUnitId = 34;           // Юридическое управление
    public const int RiskManagementOrgUnitId = 28;  // Управление риск-менеджмента
    public const int ComplianceOrgUnitId = 5;       // Управление комплаенс контроля
    public const int MethodologyOrgUnitId = 52;     // Отдел методологии
}
