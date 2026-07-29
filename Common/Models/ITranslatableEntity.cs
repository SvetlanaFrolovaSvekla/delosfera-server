namespace delosfera_server.Common.Models;

public interface ITranslatableEntity
{
    string TitleRu { get; set; }
    string? TitleEn { get; set; }
    string? TitleKg { get; set; }
}