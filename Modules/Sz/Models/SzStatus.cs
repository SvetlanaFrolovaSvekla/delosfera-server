namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Статусы служебной записки (SZ-02, уточнения ТЗ по СЗ). Хранятся строковыми кодами
/// в единой карточке документа (Document.StatusCode) — набор статусов свой у каждого контура.
/// </summary>
public static class SzStatus
{
    /// <summary>Черновик автора: виден только ему, номера ещё нет.</summary>
    public const string Draft = "Draft";

    /// <summary>
    /// На согласовании у визирующих.
    ///
    /// Согласование идёт до регистрации: номер присваивается тому, с чем уже
    /// согласились. Раньше было наоборот — записка получала номер, а потом её
    /// могли завернуть, и в книге регистрации оставался номер у документа,
    /// которого не случилось.
    /// </summary>
    public const string OnApproval = "OnApproval";

    /// <summary>Согласована, ждёт регистрации сектором делопроизводства.</summary>
    public const string PendingRegistration = "PendingRegistration";

    /// <summary>
    /// Зарегистрирована и передана подписанту.
    ///
    /// Подписант ставит подпись под согласованным и зарегистрированным текстом —
    /// последним, когда менять уже нечего.
    /// </summary>
    public const string OnSigning = "OnSigning";

    /// <summary>
    /// Прежний статус «Зарегистрирована», означавший «номер присвоен, идёт
    /// согласование». Порядок изменился, и новые записки сюда не попадают —
    /// константа остаётся ради записей, заведённых до перестройки.
    /// </summary>
    public const string Registered = "Registered";

    /// <summary>Возвращена автору на доработку.</summary>
    public const string OnRevision = "OnRevision";

    /// <summary>
    /// Подписана, ждёт решения подписанта о дальнейшем ходе.
    ///
    /// Подпись — это согласие с текстом, а не указание, что делать дальше.
    /// Дальше записка расходится: вопрос выносится на коллегиальный орган,
    /// потребность в закупке уходит в Сектор закупок, остальное идёт на
    /// исполнение. Решает это подписант — он последний, кто видел записку
    /// целиком, и выше него по ней никого нет.
    /// </summary>
    public const string OnSignerDecision = "OnSignerDecision";

    /// <summary>
    /// Вынесена на коллегиальный орган: ждёт включения в повестку и решения.
    ///
    /// Секретарь органа берёт её из «Вопросов на рассмотрение» в повестку
    /// конкретного заседания — на какое именно, система решить не может.
    /// </summary>
    public const string OnBoardReview = "OnBoardReview";

    /// <summary>
    /// Согласование пройдено, записка у адресата: решение по существу выносит он,
    /// и до этого решения записка не считается отработанной.
    /// </summary>
    public const string OnAddresseeDecision = "OnAddresseeDecision";

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
        Draft, OnApproval, PendingRegistration, OnSigning, OnSignerDecision, OnBoardReview,
        Registered, OnRevision, OnAddresseeDecision, OnExecution, Executed, Rejected,
        Withdrawn, Archived
    ];

    /// <summary>
    /// Неархивные статусы — реестр по умолчанию показывает именно их.
    ///
    /// Записка у адресата — в работе: она ждёт решения по существу, и пропасть из
    /// реестра до этого решения не может, иначе адресат её там не найдёт.
    /// </summary>
    public static readonly string[] Active =
    [
        Draft, OnApproval, PendingRegistration, OnSigning, OnSignerDecision, OnBoardReview,
        Registered, OnRevision, OnAddresseeDecision, OnExecution
    ];
}
