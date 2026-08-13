using System.ComponentModel.DataAnnotations;
using delosfera_server.Common.Validation;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class VndSearchRequest
{
    [StringLength(200)]
    public string? Code { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(200)]
    public string? RevisionText { get; set; }

    [MaxCount(20)]
    public List<string> Statuses { get; set; } = []; // "active","onact","review","consol","arch","draft"
    [MaxCount(100)]
    public List<int> TypeIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> OrganIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> DeveloperIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> ResponsibleExecutorIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> KeywordIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> RubricIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> SecrecyLevelIds { get; set; } = [];
    [MaxCount(100)]
    public List<int> UserGroupIds { get; set; } = [];
    
    [MaxCount(100)]
    /// <summary>Фильтр по инициатору (пользователь, создавший документ)</summary>
    public List<int> CreatedByUserIds { get; set; } = [];

    /// <summary>Фильтр по статусу срока актуализации: "normal","approaching","critical","overdue".
    /// Пусто = без фильтра (все, включая документы без даты актуализации).</summary>
    [MaxCount(10)]
    public List<string> ActualizationBuckets { get; set; } = [];

    public DateRangeFilter? AdoptionDate { get; set; }
    [StringLength(100)]
    public string? AdoptionCode { get; set; }
    public DateRangeFilter? EffectiveDate { get; set; }
    public DateRangeFilter? RequisitesChangedDate { get; set; }
    public DateRangeFilter? RevisionChangedDate { get; set; }
    public DateRangeFilter? CancelDate { get; set; }
    [StringLength(100)]
    public string? CancelCode { get; set; }
    public DateRangeFilter? DueActualizationDate { get; set; }
    public DateRangeFilter? LastActualizationDate { get; set; }
    public DateRangeFilter? ArchivedDate { get; set; }

    /// <summary>Только ВНД, где текущий пользователь — инициатор, согласующий, либо
    /// ответственный за актуализацию/консолидацию</summary>
    public bool LinkedToMeOnly { get; set; }

    /// <summary>Для вкладки "Черновики": "mine" — только свои черновики, "others" — черновики
    /// других пользователей (требует право ViewOtherUsersDrafts). Пусто = без доп. фильтра
    /// (но пользователь без права ViewOtherUsersDrafts в любом случае не увидит чужие черновики).</summary>
    public string? DraftOwnerScope { get; set; }
}