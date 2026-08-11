namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Замечание строгого режима (TID-09): документ не движется, пока автор замечания
/// не подтвердит «Замечания устранены».
/// </summary>
public class Remark
{
    public int Id { get; set; }

    public int ResolutionId { get; set; }
    public Resolution? Resolution { get; set; }

    public required string Text { get; set; }

    public RemarkState State { get; set; } = RemarkState.Open;

    public int? ResolvedConfirmedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
