using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// История циклов актуализации ВНД - по одной записи на каждый цикл (от старта через
/// StartAsync/ConfirmStartAfterRequestAsync до завершения через PublishAsync).
///
/// В отличие от VndDocument.ActualizationResponsibleUserId (который обнуляется сразу после
/// публикации и хранит ответственного только пока цикл активен), записи этой таблицы не
/// изменяются задним числом и не удаляются - это постоянный лог "кто и когда актуализировал".
/// </summary>
public class VndActualizationRecord : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public int ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }

    /// <summary>Требовалось ли согласование в этом цикле (зафиксировано на момент старта)</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>Планировался ли сдвиг DueActualizationDate по завершении (зафиксировано на старте)</summary>
    public bool ShiftNextPeriod { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Момент перехода документа в статус "Консолидация" в рамках этого цикла
    /// (после согласования редакции — FinalizeApprovalAsync, либо сразу при загрузке
    /// редакции без согласования во время актуализации — AddRedactionAsync). Пока null -
    /// цикл ещё либо не дошёл до консолидации, либо консолидации в нём не было и не будет
    /// (это не может произойти в текущей бизнес-логике — Publish возможен только из
    /// Consolidation, — но поле остаётся nullable на случай, если запись открыта, но ещё
    /// не дошла до этой стадии).</summary>
    public DateTime? ConsolidationStartedAt { get; set; }

    // --- Заполняется в момент публикации (PublishAsync). Пока PublishedAt == null - цикл ещё идёт.
    public DateTime? PublishedAt { get; set; }
    public bool? HadChanges { get; set; }
    public DateOnly? DueActualizationDateBefore { get; set; }
    public DateOnly? DueActualizationDateAfter { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}