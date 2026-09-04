namespace delosfera_server.Modules.Documents.VND.Models;

public enum ApprovalStageKind
{
    // Legal/RiskManagement/Compliance/Methodology - старые (до перехода на динамический
    // справочник CoordinationDefaultApprover) значения для 4 исторически фиксированных этапов.
    // Оставлены только для корректного отображения уже созданных до перехода маршрутов -
    // в новых маршрутах не используются и не проверяются позиционно/по имени.
    Legal = 0,
    RiskManagement = 1,
    Compliance = 2,
    Custom = 3,           // произвольный этап, добавленный инициатором вручную (не из справочника)
    Methodology = 4,
    Fixed = 5             // этап, построенный из записи справочника CoordinationDefaultApprover
                           // (см. VndApprovalStage.CoordinationStageId) - используется для ВСЕХ
                           // обязательных этапов в новых маршрутах, независимо от их числа
}