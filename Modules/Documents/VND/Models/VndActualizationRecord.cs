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

    /// <summary>Было ли на старте цикла заявлено "без изменений" (зафиксировано на старте,
    /// см. VndDocument.ActualizationPlannedNoChanges). Если по ходу цикла всё же загрузили
    /// новую редакцию - здесь остаётся исходное значение true, это часть истории; фактический
    /// результат смотри по HadChanges после публикации.</summary>
    public bool PlannedNoChanges { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Момент, когда был выполнен шаг "Выполнить актуализацию" (см.
    /// VndDocument.ActualizationPerformed) — то есть когда RequiresApproval/ShiftNextPeriod/
    /// PlannedNoChanges выше приняли свои финальные, действующие в цикле значения. Для пути
    /// "по заявке" (ConfirmStartAfterRequestAsync) совпадает со StartedAt — там это один и тот же
    /// шаг. Для прямого старта главным редактором (StartAsync + отдельный PerformAsync) — позже
    /// StartedAt, и пока null, значения этих трёх полей в записи ещё не окончательные (по факту
    /// ShiftNextPeriod/PlannedNoChanges равны false до выполнения этого шага).</summary>
    public DateTime? PerformedAt { get; set; }

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