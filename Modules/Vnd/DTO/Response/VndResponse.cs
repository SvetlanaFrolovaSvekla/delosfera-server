namespace delosfera_server.Modules.Vnd.DTO.Response;

public class VndResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public required string Status { get; set; }

    public int TypeId { get; set; }
    public required string TypeName { get; set; }

    public int DeveloperId { get; set; }
    public required string DeveloperName { get; set; }
    public int? CuratorDeveloperId { get; set; }
    public string? CuratorDeveloperName { get; set; }

    public int OrganId { get; set; }
    public required string OrganName { get; set; }

    public List<int> ResponsibleExecutorIds { get; set; } = [];

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

    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public int SecrecyLevelId { get; set; }
    public List<int> UserGroupIds { get; set; } = [];

    public List<int> RedactionIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}