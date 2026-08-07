using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Users.Models;


namespace delosfera_server.Modules.Documents.VND.Models;

public class VndDocument : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string Code { get; set; }
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
    /// (см. VndActualizationService). Заполняется при создании ВНД.</summary>
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

    // --- Классификаторы
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