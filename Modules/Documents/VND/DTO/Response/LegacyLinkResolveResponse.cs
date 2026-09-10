namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Ответ на попытку открыть легаси-ссылку (db://documents/{code} или
/// db://attachments/{n}) по клику внутри отрендеренного docx — см.
/// VndController.ResolveLegacyLink / VndService.ResolveLegacyLinkAsync и useDocxLegacyLinks
/// на фронте.</summary>
public class LegacyLinkResolveResponse
{
    /// <summary>"vnd" — ссылка ведёт на другой документ (открыть /base-vnd/{VndId}).
    /// "attachment" — ссылка ведёт на вложение этого же документа (скачать/открыть по FileId).</summary>
    public required string Kind { get; set; } // "vnd" | "attachment"

    public int? VndId { get; set; }
    public string? Code { get; set; }

    public int? FileId { get; set; }
    public string? FileName { get; set; }
}
