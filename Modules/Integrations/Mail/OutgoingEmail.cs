using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace delosfera_server.Modules.Integrations.Mail;

/// <summary>
/// Письмо в очереди на отправку (INT-02).
///
/// Очередь ведётся в базе, а не в памяти процесса: отправка идёт через внешний relay,
/// который бывает недоступен, и при перезапуске API уведомление о задаче не должно
/// пропасть. Заодно видно, что именно и кому банк отправлял.
/// </summary>
public class OutgoingEmail
{
    public int Id { get; set; }

    public required string ToAddress { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }

    /// <summary>Уведомление, породившее письмо — для разбора «почему пришло».</summary>
    public int? NotificationId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public int Attempts { get; set; }
    public string? LastError { get; set; }

    /// <summary>
    /// Попытки исчерпаны. Письмо остаётся в очереди как след неудачи: администратор
    /// должен видеть, что уведомление до сотрудника не дошло.
    /// </summary>
    public bool Failed { get; set; }

    /// <summary>
    /// Необязательное вложение (например, Excel-план актуализации к ежемесячной сводке —
    /// см. ActualizationNotificationService). Хранится тем же способом, что и тело письма —
    /// по получателю, а не по ссылке на общий файл: очередь и так дублирует тело письма
    /// на каждого адресата, а совместное хранение одного вложения усложнило бы модель
    /// ради экономии, которая на объёмах банка не заметна.
    /// </summary>
    public byte[]? AttachmentBytes { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
}

public class OutgoingEmailConfiguration : IEntityTypeConfiguration<OutgoingEmail>
{
    public void Configure(EntityTypeBuilder<OutgoingEmail> b)
    {
        b.ToTable("outgoing_email");

        // Воркер выбирает неотправленные и незавалившиеся — индекс под эту выборку.
        b.HasIndex(x => new {x.SentAt, x.Failed, x.CreatedAt});
    }
}
