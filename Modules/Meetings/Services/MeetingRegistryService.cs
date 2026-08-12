using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
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
/// Файл собирается вручную в формат Office Open XML, без сторонней библиотеки: нужен
/// один плоский лист без формул и стилей, и тянуть ради него зависимость в сборку
/// банка — лишний риск согласований. Строки пишутся inline-строками, поэтому таблица
/// общих строк не нужна.
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

        return Build(rows);
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

    // ── сборка xlsx ──────────────────────────────────────────────────────────

    private static byte[] Build(List<string[]> rows)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes);
            Write(zip, "_rels/.rels", RootRels);
            Write(zip, "xl/workbook.xml", Workbook);
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            Write(zip, "xl/worksheets/sheet1.xml", Sheet(rows));
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Sheet(List<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        // Ширины подобраны под содержимое: тема и решение длинные, номер и дата короткие.
        sb.Append("<cols>");
        var widths = new[] { 12, 14, 18, 45, 26, 55, 26, 16, 26, 55 };
        for (var i = 0; i < widths.Length; i++)
            sb.Append($"""<col min="{i + 1}" max="{i + 1}" width="{widths[i]}" customWidth="1"/>""");
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        AppendRow(sb, 1, Header);

        for (var i = 0; i < rows.Count; i++)
            AppendRow(sb, i + 2, rows[i]);

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, string[] values)
    {
        sb.Append($"""<row r="{rowIndex}">""");

        for (var c = 0; c < values.Length; c++)
        {
            var reference = $"{Column(c)}{rowIndex}";
            sb.Append($"""<c r="{reference}" t="inlineStr"><is><t xml:space="preserve">""");
            sb.Append(SecurityElement.Escape(values[c]) ?? string.Empty);
            sb.Append("</t></is></c>");
        }

        sb.Append("</row>");
    }

    /// <summary>Буквенное имя столбца: реестр укладывается в латиницу A–Z, но запас на AA+ оставлен.</summary>
    private static string Column(int index)
    {
        var name = string.Empty;

        for (var i = index; i >= 0; i = i / 26 - 1)
            name = (char)('A' + i % 26) + name;

        return name;
    }

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private const string RootRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Реестр решений" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;
}
