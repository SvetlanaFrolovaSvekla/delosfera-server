namespace delosfera_server.Modules.Documents.VND.Models;

public enum ApprovalStageKind
{
    Legal = 0,           // Юридическое управление - фиксированный, всегда 1-й
    RiskManagement = 1,  // Управление риск-менеджмента - фиксированный, всегда 2-й
    Compliance = 2,      // Управление комплаенс-контроля - фиксированный, всегда 3-й
    Custom = 3,          // этапы, добавленные инициатором
    Methodology = 4      // Отдел методологии - фиксированный
}