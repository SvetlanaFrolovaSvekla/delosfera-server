using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Users.Models;


namespace delosfera_server.Modules.Documents.VND.Models;

public class VndDocument : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Поисковый вектор по коду и названию. Вычисляемая колонка Postgres, а не
    /// поле, которое надо помнить обновлять: рано или поздно синхронизация
    /// расходится с данными, а генерируемая колонка устареть не может.
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }

    public required string Code { get; set; }

    // ПЕРЕХОДНЫЙ ПЕРИОД (миграция "реквизиты по редакции"): TitleRu/TitleEn/TitleKg/TypeId и
    // DeveloperId/CuratorDeveloperId/OrganId/ResponsibleExecutors/AdoptionDate/AdoptionCode/
    // EffectiveDate/Period/SecrecyLevelId/Rubrics/Keywords ниже ПРОДОЛЖАЮТ существовать здесь
    // только для обратной совместимости уже существующих мест чтения/поиска (в т.ч. SearchVector
    // ниже строится по этому TitleRu). Источник правды по ним теперь — одноимённые поля на
    // VndRedaction (см. CurrentRedaction для последней редакции): при сохранении текущей
    // редакции значения зеркалируются и сюда (см. VndService.UpdateRequisitesAsync/
    // AddRedactionAsync), поэтому здесь всегда актуальное значение ТЕКУЩЕЙ редакции. Эти поля на
    // документе будут убраны отдельным финальным шагом миграции, когда все чтения переключатся
    // на редакцию — до этого момента НЕ добавляйте новую логику, которая пишет/читает их именно
    // отсюда.
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public VndStatus Status { get; set; } = VndStatus.OnActualization;

    public int TypeId { get; set; }
    public TypeVnd? Type { get; set; }
    public int DeveloperId { get; set; } // СП-разработчик
    public OrganizationUnit? Developer { get; set; }

    public int? CuratorDeveloperId { get; set; } // Куратор разработчика (User)
    public User? CuratorDeveloper { get; set; }

    public int OrganId { get; set; } // Орган утверждения
    public ApprovalBody? Organ { get; set; }

    // Ответственные исполнители - начальник выбранного СП
    public ICollection<OrganizationUnit> ResponsibleExecutors { get; set; } = new List<OrganizationUnit>();

    // --- Даты
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

    /// <summary>Периодичность плановой актуализации — нужна, чтобы уметь
    /// автоматически сдвигать DueActualizationDate после публикации редакции
    /// (см. VndActualizationService). Заполняется при создании ВНД.
    /// Переходный период — см. пометку у DeveloperId выше: актуальный источник правды теперь
    /// VndRedaction.Period.</summary>
    public ActualizationPeriod Period { get; set; }

    // --- Текущий цикл актуализации: заполняется при переходе в OnActualization,
    // сбрасывается после публикации из Consolidation
    /// <summary>Пользователь, ответственный за текущий цикл актуализации</summary>
    public int? ActualizationResponsibleUserId { get; set; }
    public User? ActualizationResponsibleUser { get; set; }

    /// <summary>Требуется ли согласование в текущем цикле актуализации</summary>
    public bool ActualizationRequiresApproval { get; set; }

    /// <summary>Сдвигать ли DueActualizationDate после публикации текущего цикла</summary>
    public bool ActualizationShiftNextPeriod { get; set; }

    /// <summary>Заявлено ли, что в этом цикле актуализация пройдёт без изменений документа —
    /// решается при старте цикла (StartAsync/ConfirmStartAfterRequestAsync). Пока true, новая
    /// редакция в рамках цикла не создаётся — либо ответственный сразу подтверждает отсутствие
    /// изменений (VndActualizationService.ConfirmNoChangesAsync), либо (если требуется
    /// согласование) существующая действующая редакция ещё раз проходит согласование без
    /// загрузки нового файла (см. послабление в VndApprovalService.StartAsync). Как только
    /// кто-то всё же загружает новую редакцию (VndService.AddRedactionAsync) - флаг сбрасывается,
    /// потому что план "без изменений" больше не в силе.</summary>
    public bool ActualizationPlannedNoChanges { get; set; }

    /// <summary>Пройден ли шаг "Выполнить актуализацию" в текущем открытом цикле — на этом шаге
    /// фиксируются финальные ActualizationShiftNextPeriod/ActualizationPlannedNoChanges (см.
    /// VndActualizationService.PerformAsync — для прямого старта главным редактором — и
    /// ConfirmStartAfterRequestAsync — для пути "по заявке", где этот шаг совмещён со стартом).
    /// Пока false — цикл формально начат (StartAsync), но ответственный ещё не выполнил
    /// "Выполнить актуализацию": в частности, загрузка новой редакции во вкладке «Редакции»
    /// заблокирована до этого момента.</summary>
    public bool ActualizationPerformed { get; set; }

    // --- Классификаторы
    // Rubrics/SecrecyLevelId/Keywords — переходный период, см. пометку у DeveloperId выше:
    // актуальный источник правды теперь одноимённые поля на VndRedaction. UserGroups сюда
    // НЕ относится — это доступ на просмотр документа, а не реквизит содержания, поэтому
    // остаётся на уровне документа (как и было).
    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();

    public int SecrecyLevelId { get; set; }
    public SecurityLevel? SecrecyLevel { get; set; }

    public ICollection<Keyword> Keywords { get; set; } = new List<Keyword>();
    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();

    public ICollection<VndRedaction> Redactions { get; set; } = new List<VndRedaction>();

    public int? CurrentRedactionId { get; set; }
    public VndRedaction? CurrentRedaction { get; set; }

    // Ссылки на другие ВНД (само-связь многие-ко-многим через явную join-сущность VndLink)
    public ICollection<VndLink> OutgoingLinks { get; set; } = new List<VndLink>();
    public ICollection<VndLink> IncomingLinks { get; set; } = new List<VndLink>();

    /// <summary>Пользователь, создавший этот ВНД (ИНИЦИАТОР) — используется для разграничения видимости
    /// черновиков ("Мои черновики" / "Черновики других пользователей")</summary>
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int DaysInArchive => Status == VndStatus.Archived && ArchivedDate.HasValue
        ? (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - ArchivedDate.Value.DayNumber)
        : 0;
}