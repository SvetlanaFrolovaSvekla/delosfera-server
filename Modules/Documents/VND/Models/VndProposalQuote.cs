namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Цитата из текста редакции в предложении по ВНД (см. <see cref="VndProposal"/>) —
/// просто фрагмент текста и (необязательно) комментарий автора к нему.</summary>
public class VndProposalQuote
{
    public int Id { get; set; }

    public int ProposalId { get; set; }
    public VndProposal? Proposal { get; set; }

    /// <summary>Порядок цитаты в предложении (с 0).</summary>
    public int SortOrder { get; set; }

    /// <summary>"ru"/"kg"/"en" — на какой языковой версии редакции выделен фрагмент.</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }

    /// <summary>Комментарий автора к этому фрагменту.</summary>
    public string? Note { get; set; }
}
