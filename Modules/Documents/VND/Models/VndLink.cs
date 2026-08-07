namespace delosfera_server.Modules.Documents.VND.Models;

public class VndLink
{
    public int Id { get; set; }
    public int SourceVndId { get; set; } // Документ, который ссылается
    public VndDocument? SourceVnd { get; set; }
    public int TargetVndId { get; set; } // Документ, на который ссылаются
    public VndDocument? TargetVnd { get; set; }
}