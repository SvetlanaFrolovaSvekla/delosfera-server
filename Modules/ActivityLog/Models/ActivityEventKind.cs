namespace delosfera_server.Modules.ActivityLog.Models;

// Типы событий, которые могут быть в журнале событий ВНД (дашборд - последняя активность)
public enum ActivityEventKind
{
    Created = 0,            // Новый документ (черновик)
    ItemAdded = 1,          // Добавлена редакция/версия/приложение
    ProcessStarted = 2,     // Запущен процесс (согласование, маршрут и т.п.)
    Approved = 3, // Выставление резолюции: согласование без изменений
    ApprovedWithComment = 4, // Выставление резолюции: согласование с изменениями
    Rejected = 5, // Выставление резолюции: отклонено
    AutoApprovedTimeout = 6, // Просрочка срока согласования редакции. Применение автоакцепта 
    RevisionNeeded = 7,  // Когда редакцию ВНД вернули на доработку 
    Resubmitted = 8, // Инициатор исправил редакцию, вернул на согласование после внесенных изменений
    HoldStarted = 9,        // Финальная выдержка / ожидание
    Finalized = 10,         // Процесс завершён
    Published = 11, // Редакция становится действующей
    RequisitesUpdated = 12, // Смена реквизитов ВНД
    DraftDeleted = 13, // Удаление черновика ВНД (см. VndService.DeleteAsync)

    /// <summary>Прямое редактирование уже загруженной редакции главным редактором (замена
    /// файлов/описания, без согласования и без создания новой редакции) — см.
    /// VndService.EditRedactionDirectlyCoreAsync.</summary>
    RedactionEdited = 15,

    /// <summary>Главный редактор добавил согласующего в уже запущенный процесс согласования —
    /// см. VndApprovalService.AddApproverAsync.</summary>
    ApproverAdded = 16,

    /// <summary>Главный редактор убрал согласующего из уже запущенного процесса согласования
    /// (этап помечен недействующим, задача снята) — см. VndApprovalService.RemoveApproverAsync.</summary>
    ApproverRemoved = 17,

    /// <summary>Служебная метка "критическое напоминание по актуализации отправлено для такого-то
    /// порога/срока" (см. ActualizationNotificationService.SendCriticalRemindersAsync) — нужна
    /// только чтобы не потерять напоминание навсегда, если ровно нужный день был пропущен
    /// (простой сервиса/деплой), и не разослать его повторно на следующий день после этого.
    /// TextRu у таких записей — служебный, не для показа: см. ActivityLogService.GetRecentAsync/
    /// GetByEntityAsync, которые сознательно исключают этот тип из пользовательской ленты.</summary>
    ActualizationReminderSent = 14,

    Other = 99 // Другое событие (разное)

    // СЗ отдельных типов событий не требуют: журнал активности выводит их из аудита
    // (ActivityLogService.AuditSlices содержит "Sz"), а не из этого enum, который
    // остался у явного потока ВНД. См. AuditActivityText.
}