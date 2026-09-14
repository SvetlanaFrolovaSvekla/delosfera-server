using System.Text.Json;

namespace delosfera_server.Modules.Users.DTO;

/// <summary>Сохранить фильтр реестра (БП-16).</summary>
public class SavedFilterSaveRequest
{
    /// <summary>Область: sz | procurement.</summary>
    public required string Scope { get; set; }
    public required string Name { get; set; }

    /// <summary>Условия фильтра — произвольный объект формы поиска.</summary>
    public JsonElement Payload { get; set; }
}

/// <summary>Сохранённый фильтр в списке и при применении.</summary>
public class SavedFilterResponse
{
    public int Id { get; set; }
    public required string Scope { get; set; }
    public required string Name { get; set; }
    public JsonElement Payload { get; set; }
    public DateTime UpdatedAt { get; set; }
}
