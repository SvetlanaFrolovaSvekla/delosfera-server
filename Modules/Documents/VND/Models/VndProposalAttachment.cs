using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Файл, приложенный к предложению по ВНД (см. <see cref="VndProposal"/>). Хранится
/// бессрочно; скачать его могут автор и получатели предложений (см. VndFileAccessAuthorizer).</summary>
public class VndProposalAttachment
{
    public int Id { get; set; }

    public int ProposalId { get; set; }
    public VndProposal? Proposal { get; set; }

    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    public DateTime CreatedAt { get; set; }
}
