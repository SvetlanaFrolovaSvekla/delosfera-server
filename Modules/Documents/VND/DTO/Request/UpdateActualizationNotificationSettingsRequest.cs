namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateActualizationNotificationSettingsRequest
{
    public bool MonthlyDigestEnabled { get; set; }
    public List<string> MonthlyDigestColumns { get; set; } = [];

    /// <summary>Показывать ли сводку в уведомлениях внутри Делосферы. Пока MonthlyDigestEnabled
    /// = true, хотя бы один из MonthlyDigestNotifyInApp/MonthlyDigestNotifyEmail обязан быть
    /// включён — иначе рассылать нечем и некуда (проверяется в UpdateSettingsAsync).</summary>
    public bool MonthlyDigestNotifyInApp { get; set; } = true;

    /// <summary>Дублировать ли сводку на почту.</summary>
    public bool MonthlyDigestNotifyEmail { get; set; } = true;

    /// <summary>Раздел "Критические напоминания" — рассылать ли их вообще.</summary>
    public bool CriticalRemindersEnabled { get; set; }

    /// <summary>Пороги в днях ДО наступления просрочки актуализации, за которые отправляется
    /// напоминание (например [30, 14, 7, 3, 1, 0]). Порядок не важен, дубли схлопываются —
    /// см. ActualizationNotificationService.FormatThresholdDays.</summary>
    public List<int> CriticalReminderDays { get; set; } = [];

    /// <summary>Показывать ли критические напоминания в уведомлениях внутри Делосферы. Та же
    /// проверка "хотя бы один канал", что и у сводки — см. MonthlyDigestNotifyInApp.</summary>
    public bool CriticalRemindersNotifyInApp { get; set; } = true;

    /// <summary>Дублировать ли критические напоминания на почту.</summary>
    public bool CriticalRemindersNotifyEmail { get; set; } = true;
}
