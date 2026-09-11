namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Подтверждение отправки единоразовой рассылки плана актуализации — см.
/// SendActualizationOneTimeMailingRequest.</summary>
public class SendActualizationOneTimeMailingResponse
{
    public int RecipientCount { get; set; }
    public List<string> RecipientNames { get; set; } = [];
}
