namespace delosfera_server.Modules.Vnd.DTO.Request;

public class VndSearchRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? RevisionText { get; set; }

    public List<string> Statuses { get; set; } = []; // "active","onact","review","consol","arch","draft"
    public List<int> TypeIds { get; set; } = [];
    public List<int> OrganIds { get; set; } = [];
    public List<int> DeveloperIds { get; set; } = [];
    public List<int> ResponsibleExecutorIds { get; set; } = [];
    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public List<int> SecrecyLevelIds { get; set; } = [];
    public List<int> UserGroupIds { get; set; } = [];

    public DateRangeFilter? AdoptionDate { get; set; }
    public string? AdoptionCode { get; set; }
    public DateRangeFilter? EffectiveDate { get; set; }
    public DateRangeFilter? RequisitesChangedDate { get; set; }
    public DateRangeFilter? RevisionChangedDate { get; set; }
    public DateRangeFilter? CancelDate { get; set; }
    public string? CancelCode { get; set; }
    public DateRangeFilter? DueActualizationDate { get; set; }
    public DateRangeFilter? LastActualizationDate { get; set; }
    public DateRangeFilter? ArchivedDate { get; set; }
}