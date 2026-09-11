namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class ActualizationNotificationSettingsResponse
{
    public bool MonthlyDigestEnabled { get; set; }

    /// <summary>Ключи колонок Excel-вложения — см. ACTUALIZATION_COLUMNS на фронте. Обязательные
    /// (fixed) колонки сюда не входят: они вкладываются всегда, независимо от этого списка.</summary>
    public List<string> MonthlyDigestColumns { get; set; } = [];
}
