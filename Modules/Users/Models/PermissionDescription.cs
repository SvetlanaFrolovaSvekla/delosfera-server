using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Users.Models;

public class PermissionDescription : ITranslatableEntity
{
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
}