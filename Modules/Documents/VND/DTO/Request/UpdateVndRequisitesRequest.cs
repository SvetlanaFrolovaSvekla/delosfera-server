namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateVndRequisitesRequest
{
    /// <summary>Какую редакцию редактируем (см. вкладки Р1/Р2/.../Рn на вкладке "Реквизиты") —
    /// если не указано, берётся текущая (действующая либо последняя) редакция ВНД. TitleRu/En/Kg
    /// и TypeId остаются общими на весь документ и от этого поля не зависят — см. VndDocument.</summary>
    public int? RedactionId { get; set; }

    public required int TypeId { get; set; }
    public required int OrganId { get; set; }

    public int? DeveloperId { get; set; }
    public int? CuratorDeveloperId { get; set; }
    public List<int> ResponsibleExecutorIds { get; set; } = [];

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public DateOnly? AdoptionDate { get; set; }
    public string? AdoptionCode { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? DueActualizationDate { get; set; }
    public DateOnly? LastActualizationDate { get; set; }
    public bool LastActualizationHadChanges { get; set; }

    public DateOnly? CancelDate { get; set; }
    public string? CancelCode { get; set; }
    public string? CancelReason { get; set; }
    public DateOnly? ArchivedDate { get; set; }

    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public int? SecrecyLevelId { get; set; }
    public List<int> UserGroupIds { get; set; } = [];
}