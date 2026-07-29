using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Vnd.Models;

public class VndRedactionAttachment
{
    public int Id { get; set; }
    public int VndRedactionId { get; set; }
    public VndRedaction? VndRedaction { get; set; }
    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }
}