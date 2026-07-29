namespace delosfera_server.Modules.Dictionaries.DTO.Response;

/// <summary>
/// Группа пользователей из справочника
/// </summary>
public class UserGroupResponse
{
    /// <summary>Уникальный идентификатор</summary>
    public int Id { get; set; }

    /// <summary>Название, разрешённое под язык текущего запроса</summary>
    public required string Name { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Пользователи, входящие в группу</summary>
    public List<UserGroupMemberResponse> Users { get; set; } = [];

    /// <summary>Дата создания записи</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата последнего обновления записи</summary>
    public DateTime UpdatedAt { get; set; }
}