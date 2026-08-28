using delosfera_server.Modules.Correspondence.Models;

namespace delosfera_server.Modules.Correspondence.Services;

/// <summary>
/// Сроки ответа по категориям писем.
///
/// Собраны в одном месте, а не рассыпаны по коду регистрации, потому что меняются
/// они не вместе с системой, а вместе с законодательством. Когда срок рассмотрения
/// обращений поменяют, править нужно здесь — и только здесь.
///
/// Значения — норматив по умолчанию. Делопроизводство вправе поставить другой срок
/// руками: в предписании регулятора срок пишут в самом документе, и он главнее
/// любого нашего умолчания.
/// </summary>
public static class LetterDeadlinePolicy
{
    /// <summary>
    /// Обращения и жалобы клиентов. Обычный порядок рассмотрения — до 14 дней;
    /// если требуется проверка с запросом сведений, срок продлевается.
    /// </summary>
    public const int ClientAppealDays = 14;

    /// <summary>Предельный срок обращения клиента при продлении с проверкой.</summary>
    public const int ClientAppealMaxDays = 30;

    /// <summary>
    /// Запросы государственных органов по счетам и операциям. Срок короткий:
    /// такие запросы приходят с собственным сроком, обычно в три рабочих дня.
    /// </summary>
    public const int BankSecrecyDays = 3;

    /// <summary>
    /// Запросы и предписания регулятора. Норматив условный: настоящий срок стоит
    /// в самом документе, и делопроизводство его вводит. Умолчание нужно только
    /// чтобы письмо не осталось вовсе без срока.
    /// </summary>
    public const int RegulatorDefaultDays = 10;

    /// <summary>Срок по умолчанию для категории. Пусто — срок не обязателен.</summary>
    public static DateOnly? DefaultDueDate(LetterCategory category, DateOnly registeredOn) =>
        category switch
        {
            LetterCategory.ClientAppeal => registeredOn.AddDays(ClientAppealDays),
            LetterCategory.BankSecrecyInquiry => registeredOn.AddDays(BankSecrecyDays),
            LetterCategory.RegulatorRequest => registeredOn.AddDays(RegulatorDefaultDays),
            LetterCategory.Claim => registeredOn.AddDays(ClientAppealDays),
            _ => null,
        };

    /// <summary>
    /// Требует ли категория контроля срока по умолчанию. Обычная переписка — нет:
    /// поставить на контроль всё значит не контролировать ничего.
    /// </summary>
    public static bool ControlledByDefault(LetterCategory category) =>
        category is LetterCategory.RegulatorRequest
            or LetterCategory.ClientAppeal
            or LetterCategory.BankSecrecyInquiry
            or LetterCategory.Claim;

    /// <summary>
    /// Проверяет, что заданный вручную срок не выходит за предел, установленный
    /// законом. Возвращает текст ошибки или null.
    /// </summary>
    public static string? ValidateDueDate(LetterCategory category, DateOnly registeredOn, DateOnly dueDate)
    {
        if (dueDate < registeredOn)
            return "Срок ответа раньше даты регистрации.";

        var days = dueDate.DayNumber - registeredOn.DayNumber;

        if (category == LetterCategory.ClientAppeal && days > ClientAppealMaxDays)
        {
            return $"Срок рассмотрения обращения клиента не может превышать " +
                   $"{ClientAppealMaxDays} дней с даты регистрации.";
        }

        return null;
    }

    /// <summary>
    /// Ограничен ли доступ к письму узким кругом. Запрос по счетам — банковская
    /// тайна: его видят делопроизводство, исполнитель и руководитель, но не все
    /// подряд, как обычную переписку.
    /// </summary>
    public static bool IsRestricted(LetterCategory category) =>
        category is LetterCategory.BankSecrecyInquiry;
}
