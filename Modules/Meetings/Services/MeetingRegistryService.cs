using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Export;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;

namespace delosfera_server.Modules.Meetings.Services;

public interface IMeetingRegistryService
{
    /// <summary>Реестр решений за период в формате Excel.</summary>
    Task<byte[]> ExportAsync(DateOnly from, DateOnly to, MeetingBody? body);
}

/// <summary>
/// Реестр решений комитетов (кнопка на главной странице раздела).
///
/// Строка реестра — поручение, а не вопрос: у одного вопроса бывает несколько
/// ответственных со своими сроками, и в отчёте они должны стоять отдельными строками.
/// </summary>
public class MeetingRegistryService : IMeetingRegistryService
{
    private static readonly string[] Header =
    [
        "№ заседания", "Дата заседания", "№ протокола", "Тема", "Докладчик",
        "Принятые решения", "Ответственный", "Срок исполнения", "Статус", "Отчёт об исполнении",
    ];

    private static readonly int[] Widths = [12, 14, 18, 45, 26, 55, 26, 16, 26, 55];

    private readonly DelosferaDbContext _db;

    public MeetingRegistryService(DelosferaDbContext db) => _db = db;

    public async Task<byte[]> ExportAsync(DateOnly from, DateOnly to, MeetingBody? body)
    {
        if (to < from)
            throw new InvalidOperationException("Дата «по» не может быть раньше даты «с»");

        var meetings = await _db.Meetings
            .Include(m => m.Items).ThenInclude(i => i.Speaker)
            .Include(m => m.Items).ThenInclude(i => i.Assignments).ThenInclude(a => a.User)
            .Where(m => m.Date >= from && m.Date <= to)
            .Where(m => body == null || m.Body == body)
            .OrderBy(m => m.Date).ThenBy(m => m.Number)
            .AsNoTracking()
            .ToListAsync();

        var rows = new List<string[]>();

        foreach (var meeting in meetings)
        foreach (var item in meeting.Items.OrderBy(i => i.Order))
        {
            if (item.Assignments.Count == 0)
            {
                // Вопрос без поручений всё равно попадает в реестр: рассмотрение
                // состоялось, и решение по нему — часть отчёта.
                rows.Add(Row(meeting, item, null));
                continue;
            }

            foreach (var assignment in item.Assignments.OrderBy(a => a.DueDate ?? DateOnly.MaxValue))
                rows.Add(Row(meeting, item, assignment));
        }

        return XlsxWorkbook.Build(new XlsxSheet
        {
            Name = "Реестр решений",
            Header = Header,
            Widths = Widths,
            Rows = rows,
        });
    }

    private static string[] Row(Meeting meeting, AgendaItem item, AgendaAssignment? assignment) =>
    [
        $"{meeting.Number:D2}",
        meeting.Date.ToString("dd.MM.yyyy"),
        item.ProtocolNumber ?? string.Empty,
        item.Topic,
        item.Speaker?.FullName ?? string.Empty,
        item.Decision ?? string.Empty,
        assignment?.User?.FullName ?? string.Empty,
        assignment?.DueDate?.ToString("dd.MM.yyyy") ?? string.Empty,
        assignment is null ? string.Empty : MeetingTitles.Status(assignment.Status),
        assignment?.Report ?? string.Empty,
    ];
}
