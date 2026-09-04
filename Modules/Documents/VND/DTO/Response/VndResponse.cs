namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public required string Status { get; set; }

    /// <summary>"Статус ВНД" (документ-уровня) — НЕ путать со Status выше ("Статус последней
    /// редакции ВНД": active/onact/review/consol/arch/draft). Ровно 3 значения: "active"
    /// (действующий), "notYetActive" (ещё не действующий — у документа была создана только
    /// ОДНА редакция за всю историю, и он ни разу не был "Active", т.е. это первый заход:
    /// Draft/Review/Consolidation/OnActualization на самой первой редакции), "arch"
    /// (архивированный). Вычисляется на чтении (не хранится в БД) — см.
    /// VndService.ComputeDocumentStatus. Пользователям без права ViewVndRegistryExtended
    /// сервер уже сворачивает "notYetActive" в "active" (см. VndService.CollapseDocumentStatus) —
    /// такие пользователи всегда видели подобные документы как "действующие" и не должны
    /// получать 3-е значение статуса ВНД.</summary>
    public required string DocumentStatus { get; set; }

    public int TypeId { get; set; }
    public required string TypeName { get; set; }

    public int DeveloperId { get; set; }
    public required string DeveloperName { get; set; }
    public int? CuratorDeveloperId { get; set; }
    public string? CuratorDeveloperName { get; set; }

    public int OrganId { get; set; }
    public required string OrganName { get; set; }

    public List<int> ResponsibleExecutorIds { get; set; } = [];

    /// <summary>Инициатор — пользователь, создавший документ. Только для отображения, не редактируется.</summary>
    public int? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

    //TODO: Сделать историю актуализации (сейчас поле сбрасывается)
    /// <summary>Ответственный за ТЕКУЩИЙ цикл актуализации. Заполняется только пока документ
    /// в статусе OnActualization/Consolidation — после публикации (PublishAsync) сбрасывается в null.
    /// Не хранит историю прошлых циклов. Только для отображения, не редактируется.</summary>
    public int? ActualizationResponsibleUserId { get; set; }
    public string? ActualizationResponsibleUserName { get; set; }

    /// <summary>Требуется ли согласование в ТЕКУЩЕМ цикле актуализации — заполнено, только
    /// пока документ в OnActualization/Review/Consolidation в рамках этого цикла.</summary>
    public bool ActualizationRequiresApproval { get; set; }

    /// <summary>Заявлено ли, что текущий цикл актуализации пройдёт без изменений документа.</summary>
    public bool ActualizationPlannedNoChanges { get; set; }

    /// <summary>Сдвигать ли DueActualizationDate после публикации текущего цикла — зафиксировано
    /// на шаге "Выполнить актуализацию" (см. ActualizationPerformed). Пока этот шаг не пройден,
    /// значение ещё не окончательное.</summary>
    public bool ActualizationShiftNextPeriod { get; set; }

    /// <summary>Пройден ли шаг "Выполнить актуализацию" в текущем открытом цикле — пока false,
    /// значения ActualizationPlannedNoChanges/сдвига срока ещё не окончательные, и загрузка новой
    /// редакции заблокирована (см. VndDocument.ActualizationPerformed).</summary>
    public bool ActualizationPerformed { get; set; }

    public DateOnly? AdoptionDate { get; set; }
    public string? AdoptionCode { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? RequisitesChangedDate { get; set; }
    public DateOnly? RevisionChangedDate { get; set; }
    public DateOnly? CancelDate { get; set; }
    public string? CancelCode { get; set; }
    public string? CancelReason { get; set; }
    public DateOnly? ArchivedDate { get; set; }
    public DateOnly? DueActualizationDate { get; set; }
    public DateOnly? LastActualizationDate { get; set; }
    public bool LastActualizationHadChanges { get; set; }
    public int DaysInArchive { get; set; }

    /// <summary>статусы актуализации: "normal" | "approaching" | "critical" | "overdue" | null (нет даты актуализации)</summary>
    public string? ActualizationBucket { get; set; }

    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public int SecrecyLevelId { get; set; }
    public List<int> UserGroupIds { get; set; } = [];

    public List<int> RedactionIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Виды связи текущего пользователя с этим документом ("initiator",
    /// "currentApprover", "pastApprover", "currentActualizer", "pastActualizer",
    /// "currentConsolidator", "pastConsolidator") — заполняется только когда поиск идёт
    /// с LinkedToMeOnly=true, иначе пустой список (не вычисляется, чтобы не тратить время
    /// на поиск, где фильтр не запрашивался).</summary>
    public List<string> LinkedToMeRelations { get; set; } = [];
}