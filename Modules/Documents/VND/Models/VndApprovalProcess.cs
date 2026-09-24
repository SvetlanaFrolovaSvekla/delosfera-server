using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

public class VndApprovalProcess : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    /// <summary>Редакция, которая проходит согласование</summary>
    public int RedactionId { get; set; }
    public VndRedaction? Redaction { get; set; }

    public int InitiatorUserId { get; set; }

    /// <summary>Процесс запущен в рамках "актуализации без изменений" (см.
    /// VndDocument.ActualizationPlannedNoChanges): новая редакция не создавалась, на повторное
    /// согласование ушла уже действующая, ранее согласованная редакция. Влияет на подпись Листа
    /// согласования, отображение процесса в истории/ходе согласования и на то, во что
    /// возвращается редакция при отзыве/отклонении (она остаётся действующей, а не черновиком).</summary>
    public bool IsNoChangesActualization { get; set; }

    // --- Текущий статус ВНД (Primary/RevisionNeeded/Repeated/FinalHold/Approved/Cancelled/Rejected)
    public ApprovalProcessStatus Status { get; set; } = ApprovalProcessStatus.Primary;

    // --- Нормативы в минутах. Отсчёт каждого — от момента старта именно этого этапа
    public int PrimaryDeadlineMinutes  { get; set; } // Первичная выдержка 
    public int RepeatDeadlineMinutes  { get; set; } // Согласование после устранения замечаний
    public int FinalHoldDeadlineMinutes  { get; set; } // Финальная выдержка

    /// <summary>Нормативы — в РАБОЧИХ минутах (пн–пт 09:00–18:00 по Бишкеку без праздников из
    /// справочника "Производственный календарь", 1 д. = 540 мин — см. VndWorkingCalendar).
    /// false — процесс запущен до перехода на рабочий календарь: его нормативы остаются
    /// календарными (1 д. = 1440 мин), чтобы срок уже идущего согласования не сдвинулся.</summary>
    public bool UsesWorkingTime { get; set; }

    // Моменты отсчёта выдержек
    public DateTime PrimaryStartedAt { get; set; }
    public DateTime? RepeatStartedAt { get; set; }
    public DateTime? FinalHoldStartedAt { get; set; }
    
    public string? RepeatInitiatorComment { get; set; }

    /// <summary>Файлы, приложенные инициатором к RepeatInitiatorComment — полностью
    /// перезаписываются при каждой повторной отправке (см. ResubmitAfterRevisionAsync).</summary>
    public ICollection<VndRepeatCommentAttachment> RepeatInitiatorCommentAttachments { get; set; } = new List<VndRepeatCommentAttachment>();

    public DateTime? CompletedAt { get; set; } // Когда процесс завершился, ВНД стал действующим

    public ICollection<VndApprovalStage> Stages { get; set; } = new List<VndApprovalStage>();

    /// <summary>Снимки завершённых кругов "Повторного согласования"/"Финальной выдержки" -
    /// см. <see cref="VndApprovalPhaseRound"/>. Текущий (ещё не перезаписанный) круг в этой
    /// коллекции не участвует - он всегда виден напрямую через Repeat*/FinalHold* поля
    /// Stages.</summary>
    public ICollection<VndApprovalPhaseRound> PhaseRounds { get; set; } = new List<VndApprovalPhaseRound>();

    /// <summary>Снимки файлов редакции, сделанные перед каждой перезаписью при повторных
    /// отправках после замечаний — см. <see cref="VndRedactionRevisionSnapshot"/>. Нужно,
    /// чтобы проверяющие могли скачать и сравнить версию документа, к которой относились их
    /// замечания, с исправленной.</summary>
    public ICollection<VndRedactionRevisionSnapshot> RedactionSnapshots { get; set; } = new List<VndRedactionRevisionSnapshot>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Конкретные дедлайны для согласующих. Хранятся в БД (а не вычисляются "старт + минуты"),
    // потому что с рабочим календарём срок зависит от праздников - считается один раз при
    // старте фазы (VndApprovalDeadlines.Apply) и пересчитывается только при правке
    // производственного календаря. Заодно фоновая проверка просрочек фильтрует по ним прямо в SQL.
    public DateTime PrimaryDeadlineAt { get; set; }
    public DateTime? RepeatDeadlineAt { get; set; }
    public DateTime? FinalHoldDeadlineAt { get; set; }
    
    // Матрица разногласий
    public ICollection<VndDisagreementMatrixRow> DisagreementMatrixRows { get; set; } = new List<VndDisagreementMatrixRow>();
}