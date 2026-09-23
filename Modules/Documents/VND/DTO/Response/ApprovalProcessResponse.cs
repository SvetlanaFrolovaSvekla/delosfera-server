namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class ApprovalProcessResponse
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public int RedactionId { get; set; }
    public int InitiatorUserId { get; set; }
    public required string InitiatorName { get; set; }

    /// <summary>Должность инициатора (если назначена)</summary>
    public string? InitiatorPosition { get; set; }

    /// <summary>Процесс запущен в рамках актуализации без изменений - на согласование ушла уже
    /// действующая редакция (см. VndApprovalProcess.IsNoChangesActualization).</summary>
    public bool IsNoChangesActualization { get; set; }

    /// <summary>Лист согласования, сформированный по итогам ИМЕННО этого процесса (null, пока
    /// процесс не завершён согласованием) - у редакции их может быть несколько.</summary>
    public int? ApprovalSheetFileId { get; set; }
    public string? ApprovalSheetFileName { get; set; }
    public required string Status { get; set; } // primary/revision_needed/repeated/final_hold/approved/cancelled

    public string? RepeatInitiatorComment { get; set; }

    /// <summary>Файлы, приложенные инициатором к RepeatInitiatorComment. Остаются доступны и
    /// после согласования редакции — часть истории согласования.</summary>
    public List<ApprovalStageAttachmentResponse> RepeatInitiatorCommentAttachments { get; set; } = [];

    public int PrimaryDeadlineMinutes { get; set; }
    public int RepeatDeadlineMinutes { get; set; }
    public int FinalHoldDeadlineMinutes { get; set; }

    public DateTime PrimaryStartedAt { get; set; }
    public DateTime PrimaryDeadlineAt { get; set; }
    public DateTime? RepeatStartedAt { get; set; }
    public DateTime? RepeatDeadlineAt { get; set; }
    public DateTime? FinalHoldStartedAt { get; set; }
    public DateTime? FinalHoldDeadlineAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<DisagreementMatrixRowResponse> DisagreementMatrixRows { get; set; } = [];

    public List<ApprovalStageResponse> Stages { get; set; } = [];

    /// <summary>История ЗАВЕРШЁННЫХ (уже перезаписанных следующим) кругов фаз "Повторное
    /// согласование"/"Финальная выдержка" - см. VndApprovalPhaseRound. Текущий/последний круг
    /// сюда не входит, он виден напрямую через Repeat*/FinalHold* поля на Stages выше. Нужно
    /// клиенту, чтобы построить полную "Историю маршрута согласования" (карусель схем по
    /// каждому кругу) - без этого при нескольких кругах доработки подряд в одном и том же
    /// процессе согласования видно было бы только решения последнего круга.</summary>
    public List<ApprovalPhaseRoundResponse> PhaseRounds { get; set; } = [];

    /// <summary>Снимки файлов редакции, сделанные перед каждой перезаписью при повторных
    /// отправках после замечаний — см. VndRedactionRevisionSnapshot. Нужно клиенту для кнопок
    /// "Скачать версию" в "Истории маршрута согласования" (см.
    /// ApprovalRouteHistoryCarousel.tsx) — по одному снимку на прошлый круг/этап, чтобы можно
    /// было сравнить версию документа, к которой относились замечания, с исправленной.</summary>
    public List<VndRedactionRevisionSnapshotResponse> RedactionSnapshots { get; set; } = [];

    /// <summary>ВСЕ цитаты процесса, по всем этапам/фазам и ВСЕМ версиям документа редакции
    /// (включая уже вытесненные последующими повторными отправками) — в отличие от
    /// Primary/Repeat/FinalHoldQuotes на <see cref="ApprovalStageResponse"/> (которые содержат
    /// только цитаты ТЕКУЩЕЙ/живой версии документа), нужен клиенту, чтобы при просмотре/
    /// сравнении конкретной прошлой версии ("Р1.1", "Р1.2" и т.д.) во время активного
    /// согласования показать замечания именно этой версии — см. ApprovalStageQuoteResponse.RevisionIndex,
    /// VndRedactionRevisionSnapshotResponse.</summary>
    public List<ApprovalStageQuoteResponse> AllQuotes { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Один завершённый круг фазы "Повторное согласование" (Phase == "repeat") или
/// "Финальная выдержка" (Phase == "finalHold") - см. VndApprovalPhaseRound.</summary>
public class ApprovalPhaseRoundResponse
{
    public int Id { get; set; }

    /// <summary>"repeat"/"finalHold"</summary>
    public required string Phase { get; set; }

    /// <summary>Номер круга внутри этой фазы этого процесса, начиная с 1.</summary>
    public int RoundNumber { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    /// <summary>Комментарий инициатора об исправлениях на этом круге - заполнен только для
    /// Phase == "repeat".</summary>
    public string? InitiatorComment { get; set; }

    public List<ApprovalPhaseRoundStageDecisionResponse> StageDecisions { get; set; } = [];
}

/// <summary>Решение одного согласующего в рамках одного завершённого круга.</summary>
public class ApprovalPhaseRoundStageDecisionResponse
{
    /// <summary>Id этапа (ApprovalStageResponse.Id), к которому относится это решение.</summary>
    public int StageId { get; set; }

    public required string Decision { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>Файлы, приложенные согласующим к решению ЭТОГО круга (см.
    /// VndApprovalStageAttachment.PhaseRoundId) - раньше удалялись при переходе к следующему
    /// кругу, теперь сохраняются в истории.</summary>
    public List<ApprovalStageAttachmentResponse> Attachments { get; set; } = [];
}

public class ApprovalStageResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public required string Kind { get; set; }

    /// <summary>Название этапа - снимок на момент запуска согласования (см.
    /// VndApprovalStage.Title). Для маршрутов, построенных до перехода на динамический
    /// справочник, выводится из Kind.</summary>
    public required string Title { get; set; }

    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public int ApproverUserId { get; set; }
    public required string ApproverName { get; set; }

    /// <summary>Убран главным редактором из уже запущенного процесса согласования (см.
    /// VndApprovalStage.IsRemovedByEditor) — этап остаётся в маршруте (история согласования не
    /// теряется), но недействующий: задача с него снята и он больше не участвует в согласовании.
    /// Клиент показывает такой этап как "Недействующий (убран главным редактором)" независимо
    /// от значения полей *Decision ниже.</summary>
    public bool IsRemovedByEditor { get; set; }

    public required string PrimaryDecision { get; set; }
    public string? PrimaryComment { get; set; }
    public DateTime? PrimaryDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> PrimaryAttachments { get; set; } = [];
    public List<ApprovalStageQuoteResponse> PrimaryQuotes { get; set; } = [];

    public bool ParticipatesInRepeat { get; set; }

    public string? RepeatDecision { get; set; }
    public string? RepeatComment { get; set; }
    public DateTime? RepeatDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> RepeatAttachments { get; set; } = [];
    public List<ApprovalStageQuoteResponse> RepeatQuotes { get; set; } = [];

    public string? FinalHoldDecision { get; set; }
    public string? FinalHoldComment { get; set; }
    public DateTime? FinalHoldDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> FinalHoldAttachments { get; set; } = [];
    public List<ApprovalStageQuoteResponse> FinalHoldQuotes { get; set; } = [];
}

/// <summary>Файл, приложенный согласующим к резолюции. Остаётся доступен и после того, как
/// редакция станет согласованной — часть истории согласования наравне с текстом резолюции.</summary>
public class ApprovalStageAttachmentResponse
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
}

/// <summary>Цитата из текста редакции, на которую согласующий сослался в резолюции — см.
/// VndApprovalStageQuote на бэке. Комментарий/замечание к этой цитате отдельно не приходит:
/// это вся резолюция фазы (Primary/Repeat/FinalHoldComment), в которую эта цитата попадает.</summary>
public class ApprovalStageQuoteResponse
{
    public int Id { get; set; }

    /// <summary>Id этапа (ApprovalStageResponse.Id), к которому относится цитата — нужен, когда
    /// цитата приходит вне контекста конкретного этапа (см. ApprovalProcessResponse.AllQuotes).</summary>
    public int StageId { get; set; }

    /// <summary>"primary"/"repeat"/"finalHold" — фаза решения, к которой относится цитата.</summary>
    public required string Phase { get; set; }

    /// <summary>"ru"/"kg"/"en"/"tid"/"approvalSheet"/"disagreementMatrix"</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }

    /// <summary>Версия документа редакции, к которой относится цитата - 0 для самой первой
    /// поданной версии ("Р1"), 1 для "Р1.1" и т.д. — см. VndApprovalStageQuote.RevisionIndex.</summary>
    public int RevisionIndex { get; set; }

    /// <summary>"Якорь" цитаты - контекст до/после и номер вхождения (см.
    /// VndApprovalStageQuote.Prefix/Suffix/Occurrence). null у старых цитат.</summary>
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public int? Occurrence { get; set; }

    /// <summary>Замечание согласующего именно к этому фрагменту - см. VndApprovalStageQuote.Note.</summary>
    public string? Note { get; set; }
}

/// <summary>Снимок файлов редакции, сохранённый перед тем, как инициатор перезаписал их
/// очередной повторной отправкой после замечаний — см. VndRedactionRevisionSnapshot на бэке.
/// Позволяет проверяющим скачать и сравнить версию документа, к которой относились их
/// замечания, с исправленной. Номеруется последовательно в рамках редакции: "10296-Р1.1",
/// "10296-Р1.2" и т.д. Файловые поля здесь null, если в соответствующей редакции документ на
/// этом языке/этот файл отсутствовал вовсе на момент снимка (см. одноимённые поля на
/// VndRedaction).</summary>
public class VndRedactionRevisionSnapshotResponse
{
    public int Id { get; set; }

    /// <summary>Порядковый номер снимка в рамках редакции, начиная с 1.</summary>
    public int SnapshotNumber { get; set; }

    /// <summary>"primary"/"repeat"/"finalHold" — фаза круга, чьи замечания относятся к этой
    /// версии файлов.</summary>
    public required string Phase { get; set; }

    /// <summary>Номер круга внутри фазы — null для Phase == "primary".</summary>
    public int? RoundNumber { get; set; }

    public int? DocFileRuId { get; set; }
    public string? DocFileRuName { get; set; }
    public int? DocFileKgId { get; set; }
    public string? DocFileKgName { get; set; }
    public int? DocFileEnId { get; set; }
    public string? DocFileEnName { get; set; }
    public int? TidFileId { get; set; }
    public string? TidFileName { get; set; }
    public int? DisagreementMatrixFileId { get; set; }
    public string? DisagreementMatrixFileName { get; set; }

    public DateTime CreatedAt { get; set; }
}