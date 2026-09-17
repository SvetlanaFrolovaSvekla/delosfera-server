namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class ActualizationNotificationSettingsResponse
{
    public bool MonthlyDigestEnabled { get; set; }

    /// <summary>Ключи колонок Excel-вложения — см. ACTUALIZATION_COLUMNS на фронте. Обязательные
    /// (fixed) колонки сюда не входят: они вкладываются всегда, независимо от этого списка.</summary>
    public List<string> MonthlyDigestColumns { get; set; } = [];

    /// <summary>Показывать ли сводку в уведомлениях внутри Делосферы.</summary>
    public bool MonthlyDigestNotifyInApp { get; set; }

    /// <summary>Дублировать ли сводку на почту.</summary>
    public bool MonthlyDigestNotifyEmail { get; set; }

    /// <summary>Раздел "Критические напоминания" — рассылаются ли они вообще.</summary>
    public bool CriticalRemindersEnabled { get; set; }

    /// <summary>Пороги в днях ДО наступления просрочки актуализации, за которые отправляется
    /// напоминание, по возрастанию.</summary>
    public List<int> CriticalReminderDays { get; set; } = [];

    /// <summary>Показывать ли критические напоминания в уведомлениях внутри Делосферы.</summary>
    public bool CriticalRemindersNotifyInApp { get; set; }

    /// <summary>Дублировать ли критические напоминания на почту.</summary>
    public bool CriticalRemindersNotifyEmail { get; set; }
}
