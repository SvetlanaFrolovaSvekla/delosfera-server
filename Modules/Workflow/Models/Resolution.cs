namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Резолюция участника (TID-07). Подписывается ЭП (SignatureId).
/// </summary>
public class Resolution
{
    public int Id { get; set; }

    public int RouteParticipantId { get; set; }
    public RouteParticipant? RouteParticipant { get; set; }

    public ResolutionType Type { get; set; }

    /// <summary>Комментарий/причина (обязателен для замечаний и отклонения).</summary>
    public string? Comment { get; set; }

    /// <summary>Подпись (Signing.Signature) хеша версии.</summary>
    public int? SignatureId { get; set; }

    public DateTime At { get; set; }

    public ICollection<Remark> Remarks { get; set; } = new List<Remark>();
}
