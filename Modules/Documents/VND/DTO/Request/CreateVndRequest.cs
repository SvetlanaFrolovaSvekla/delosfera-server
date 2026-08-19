using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class CreateVndRequest
{
    [Range(1, int.MaxValue)]
    public required int TypeId { get; set; }

    [Range(1, int.MaxValue)]
    public required int OrganId { get; set; }

    /// <summary>Разработчик (СП). Если null — берём OrgUnitId текущего пользователя</summary>
    public int? DeveloperId { get; set; }

    /// <summary>Куратор разработчика. Если null — берём CuratorUserId у Developer</summary>
    public int? CuratorDeveloperId { get; set; }

    /// <summary>Ответственные исполнители (СП). Если пусто — [DeveloperId текущего пользователя]</summary>
    public List<int> ResponsibleExecutorIds { get; set; } = [];

    [Required, StringLength(500, MinimumLength = 1)]
    public required string TitleRu { get; set; }

    [StringLength(500)]
    public string? TitleEn { get; set; }

    [StringLength(500)]
    public string? TitleKg { get; set; }

    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];
    public int? SecrecyLevelId { get; set; }
    public List<int> UserGroupIds { get; set; } = [];

    public required ActualizationPeriod Period { get; set; }

    /// <summary>Обязательно, если Period == Custom</summary>
    public DateOnly? DueActualizationDate { get; set; }
}