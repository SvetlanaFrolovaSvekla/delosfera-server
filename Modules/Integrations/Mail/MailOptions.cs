namespace delosfera_server.Modules.Integrations.Mail;

/// <summary>
/// Корпоративная почта для исходящих уведомлений (INT-02).
/// </summary>
public class MailOptions
{
    public const string Section = "Mail";

    /// <summary>Выключено — письма не ставятся в очередь вовсе, уведомления остаются в системе.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }

    /// <summary>Учётные данные relay. Пусто — отправка без аутентификации (внутренний relay банка).</summary>
    public string? User { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "sed@keremetbank.kg";
    public string FromName { get; set; } = "СЭД Керемет Банк";

    /// <summary>
    /// Адрес системы для ссылок в письмах. Без него письмо сообщает о задаче,
    /// но не даёт по ней перейти.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5174";

    /// <summary>Сколько раз пробовать отправить письмо, прежде чем признать неудачу.</summary>
    public int MaxAttempts { get; set; } = 5;
}
