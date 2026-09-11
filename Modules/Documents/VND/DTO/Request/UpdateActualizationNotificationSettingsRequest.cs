namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateActualizationNotificationSettingsRequest
{
    public bool MonthlyDigestEnabled { get; set; }
    public List<string> MonthlyDigestColumns { get; set; } = [];

    /// <summary>Раздел "Критические напоминания" — рассылать ли их вообще.</summary>
    public bool CriticalRemindersEnabled { get; set; }

    /// <summary>Пороги в днях ДО наступления просрочки актуализации, за которые отправляется
    /// напоминание (например [30, 14, 7, 3, 1, 0]). Порядок не важен, дубли схлопываются —
    /// см. ActualizationNotificationService.FormatThresholdDays.</summary>
    public List<int> CriticalReminderDays { get; set; } = [];
}
