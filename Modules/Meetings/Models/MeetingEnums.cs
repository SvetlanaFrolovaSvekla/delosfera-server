namespace delosfera_server.Modules.Meetings.Models;

/// <summary>
/// Коллегиальный орган, чьё заседание протоколируется.
///
/// Нумерация заседаний и протоколов ведётся отдельно по каждому органу, поэтому
/// орган — не справочник с произвольными значениями, а фиксированный перечень:
/// от него зависят и счётчик, и права секретаря, и адресаты уведомлений.
/// </summary>
public enum MeetingBody
{
    /// <summary>Правление Банка.</summary>
    Board = 1,

    /// <summary>Комитет по проблемным активам.</summary>
    Kpa = 2,

    /// <summary>Кредитный комитет (КИТ).</summary>
    CreditCommittee = 3,
}

/// <summary>Форма проведения заседания.</summary>
public enum MeetingForm
{
    /// <summary>Очно.</summary>
    InPerson = 1,

    /// <summary>Заочно.</summary>
    Absentee = 2,
}

/// <summary>
/// Статус исполнения поручения по вопросу повестки.
///
/// Разделение «исполнено в срок» и «исполнено с нарушением срока» сделано значением
/// статуса, а не вычислением по датам: решение о том, засчитан ли срок, принимает
/// секретарь, и оно должно оставаться неизменным после закрытия поручения.
/// </summary>
public enum ExecutionStatus
{
    New = 1,
    InProgress = 2,
    DoneOnTime = 3,
    DoneLate = 4,
    NotDone = 5,
    Cancelled = 6,

    /// <summary>Исключено/снято из повестки дня заседания.</summary>
    Excluded = 7,
}

/// <summary>Назначение файла, приложенного к вопросу повестки.</summary>
public enum MeetingFileKind
{
    /// <summary>Служебная записка, которой инициирован вопрос.</summary>
    Sz = 1,

    /// <summary>Протокол заседания.</summary>
    Protocol = 2,

    /// <summary>Файлы об исполнении — доступны всем сотрудникам.</summary>
    Execution = 3,
}
