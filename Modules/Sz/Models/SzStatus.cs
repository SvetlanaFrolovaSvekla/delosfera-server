namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Статусы служебной записки (SZ-02, уточнения ТЗ по СЗ). Хранятся строковыми кодами
/// в единой карточке документа (Document.StatusCode) — набор статусов свой у каждого контура.
/// </summary>
public static class SzStatus
{
    /// <summary>Черновик автора: виден только ему, номера ещё нет.</summary>
    public const string Draft = "Draft";

    /// <summary>Отправлена, ждёт регистрации сектором делопроизводства.</summary>
    public const string PendingRegistration = "PendingRegistration";

    /// <summary>Зарегистрирована: присвоен номер, маршрут согласования запущен.</summary>
    public const string Registered = "Registered";

    /// <summary>Возвращена автору на доработку.</summary>
    public const string OnRevision = "OnRevision";

    /// <summary>Согласована и передана на исполнение.</summary>
    public const string OnExecution = "OnExecution";

    /// <summary>Исполнена.</summary>
    public const string Executed = "Executed";

    /// <summary>Забракована.</summary>
    public const string Rejected = "Rejected";

    /// <summary>Отозвана автором.</summary>
    public const string Withdrawn = "Withdrawn";

    /// <summary>В архиве.</summary>
    public const string Archived = "Archived";

    /// <summary>Все коды — для валидации фильтров реестра.</summary>
    public static readonly string[] All =
    [
        Draft, PendingRegistration, Registered, OnRevision,
        OnExecution, Executed, Rejected, Withdrawn, Archived
    ];

    /// <summary>Неархивные статусы — реестр по умолчанию показывает именно их.</summary>
    public static readonly string[] Active =
    [
        Draft, PendingRegistration, Registered, OnRevision, OnExecution
    ];
}
