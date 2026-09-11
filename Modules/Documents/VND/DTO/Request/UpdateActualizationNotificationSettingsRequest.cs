namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateActualizationNotificationSettingsRequest
{
    public bool MonthlyDigestEnabled { get; set; }
    public List<string> MonthlyDigestColumns { get; set; } = [];
}
