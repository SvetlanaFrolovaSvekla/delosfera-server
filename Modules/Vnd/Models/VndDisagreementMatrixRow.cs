using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Vnd.Models;

/// <summary>Строка матрицы разногласий по замечаниям к редакции.
/// Заполняется вручную инициатором, пока процесс в статусе RevisionNeeded.
/// Живёт весь жизненный цикл процесса согласования (переживает несколько кругов доработки).</summary>
public class VndDisagreementMatrixRow : IAuditableEntity
{
    public int Id { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }

    /// <summary>Редакция (позиция) разработчика</summary>
    public required string DeveloperPosition { get; set; }

    /// <summary>Редакция и комментарий оппонента (согласующего)</summary>
    public required string OpponentPosition { get; set; }

    /// <summary>Комментарий (обоснование) разработчика</summary>
    public string? DeveloperJustification { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}