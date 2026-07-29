using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Vnd.Models;

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

    // --- Классификаторы
    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();

    public int SecrecyLevelId { get; set; }
    public SecurityLevel? SecrecyLevel { get; set; }

    public ICollection<Keyword> Keywords { get; set; } = new List<Keyword>();
    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();

    public ICollection<VndRedaction> Redactions { get; set; } = new List<VndRedaction>();

    // Ссылки на другие ВНД (само-связь многие-ко-многим через явную join-сущность VndLink)
    public ICollection<VndLink> OutgoingLinks { get; set; } = new List<VndLink>();
    public ICollection<VndLink> IncomingLinks { get; set; } = new List<VndLink>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int DaysInArchive => Status == VndStatus.Archived && ArchivedDate.HasValue
        ? (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - ArchivedDate.Value.DayNumber)
        : 0;
}