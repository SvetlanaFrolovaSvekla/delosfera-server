using System.Text;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Analytics.Services;

public interface IDigestEmailService
{
    /// <summary>Разослать утренний дайджест тем, у кого есть открытые задачи (УВ-15).</summary>
    Task<int> SendDailyAsync(CancellationToken ct = default);
}

/// <summary>
/// Утренняя рассылка дайджеста (УВ-15). Собирает получателей — тех, у кого есть открытые
/// задачи или незакрытые листы ознакомления, — и каждому кладёт в почтовую очередь
/// короткую сводку из DigestService. Само письмо отправит MailWorker.
/// </summary>
public class DigestEmailService : IDigestEmailService
{
    private readonly DelosferaDbContext _db;
    private readonly IDigestService _digest;
    private readonly IMailQueue _mail;
    private readonly ILogger<DigestEmailService> _logger;

    public DigestEmailService(
        DelosferaDbContext db, IDigestService digest, IMailQueue mail, ILogger<DigestEmailService> logger)
    {
        _db = db;
        _digest = digest;
        _mail = mail;
        _logger = logger;
    }

    public async Task<int> SendDailyAsync(CancellationToken ct = default)
    {
        if (!_mail.Enabled) return 0;

        // Получатели — у кого есть открытая работа. Иначе слать нечего.
        var wfUsers = await _db.WorkflowTasks
            .Where(t => t.State == WorkflowTaskState.Open)
            .Select(t => t.AssigneeUserId)
            .Distinct()
            .ToListAsync(ct);

        var ackUsers = await _db.AcknowledgementEntries
            .Where(e => e.State == AcknowledgementState.Pending && e.Sheet!.ClosedAt == null)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync(ct);

        var recipients = wfUsers.Concat(ackUsers).Distinct().ToList();

        var sent = 0;
        foreach (var uid in recipients)
        {
            ct.ThrowIfCancellationRequested();

            var digest = await _digest.GetAsync(uid);
            if (digest.Total == 0) continue;

            // Именованный ct: чтобы вызов совпадал и с 6-, и с 7-параметровой версией
            // EnqueueAsync (у неё между notificationId и ct есть опциональное вложение).
            await _mail.EnqueueAsync([uid], "Ваш дайджест на сегодня", BuildBody(digest), "/digest", null, ct: ct);
            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Дайджест: поставлено в очередь {Count} писем", sent);

        return sent;
    }

    private static string BuildBody(DTO.DigestDto d)
    {
        var sb = new StringBuilder();
        sb.Append($"Открытых задач: {d.Total}");
        if (d.Overdue > 0) sb.Append($", просрочено: {d.Overdue}");
        if (d.DueSoon > 0) sb.Append($", срок в ближайшие сутки-двое: {d.DueSoon}");
        sb.Append('.');

        if (d.OverdueItems.Count > 0)
        {
            sb.Append("\n\nЧто горит:");
            foreach (var t in d.OverdueItems)
                sb.Append($"\n• {t.DocumentTypeTitle}: {t.DocumentTitle} ({t.TaskType})");
        }

        if (d.Upcoming.Count > 0)
        {
            sb.Append("\n\nНа подходе:");
            foreach (var t in d.Upcoming)
                sb.Append($"\n• {t.DocumentTypeTitle}: {t.DocumentTitle} ({t.TaskType})");
        }

        return sb.ToString();
    }
}
