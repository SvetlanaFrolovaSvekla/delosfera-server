using System.IO.Compression;
using System.Security;
using System.Text;

namespace delosfera_server.Common.Export;

/// <summary>Лист книги: название, заголовки колонок, ширины и строки.</summary>
public class XlsxSheet
{
    public required string Name { get; set; }
    public required string[] Header { get; set; }
    public List<string[]> Rows { get; set; } = [];

    /// <summary>Ширины колонок. Не заданы — берётся значение по умолчанию.</summary>
    public int[]? Widths { get; set; }
}

/// <summary>
/// Сборка книги Office Open XML без сторонних библиотек.
///
/// Выгрузки банка — плоские таблицы без формул и стилей, и ради них тянуть в сборку
/// зависимость с отдельным согласованием незачем. Строки пишутся inline-строками,
/// поэтому таблица общих строк не нужна.
///
/// Общий код вынесен сюда: реестр решений, статистика по запискам и аналитика
/// собираются одинаково, и расхождение в трёх копиях этого кода — вопрос времени.
/// </summary>
public static class XlsxWorkbook
{
    private const int DefaultWidth = 22;

    public static byte[] Build(params XlsxSheet[] sheets)
    {
        if (sheets.Length == 0)
            throw new ArgumentException("Книга без листов не собирается", nameof(sheets));

        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes(sheets.Length));
            Write(zip, "_rels/.rels", RootRels);
            Write(zip, "xl/workbook.xml", Workbook(sheets));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Length));

            for (var i = 0; i < sheets.Length; i++)
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i]));
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Sheet(XlsxSheet sheet)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        sb.Append("<cols>");
        for (var i = 0; i < sheet.Header.Length; i++)
        {
            var width = sheet.Widths is not null && i < sheet.Widths.Length ? sheet.Widths[i] : DefaultWidth;
            sb.Append($"""<col min="{i + 1}" max="{i + 1}" width="{width}" customWidth="1"/>""");
        }
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        AppendRow(sb, 1, sheet.Header);

        for (var i = 0; i < sheet.Rows.Count; i++)
            AppendRow(sb, i + 2, sheet.Rows[i]);

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, string[] values)
    {
        sb.Append($"""<row r="{rowIndex}">""");

        for (var c = 0; c < values.Length; c++)
        {
            sb.Append($"""<c r="{Column(c)}{rowIndex}" t="inlineStr"><is><t xml:space="preserve">""");
            sb.Append(SecurityElement.Escape(values[c] ?? string.Empty) ?? string.Empty);
            sb.Append("</t></is></c>");
        }

        sb.Append("</row>");
    }

    /// <summary>Буквенное имя столбца с запасом на AA и дальше.</summary>
    private static string Column(int index)
    {
        var name = string.Empty;

        for (var i = index; i >= 0; i = i / 26 - 1)
            name = (char)('A' + i % 26) + name;

        return name;
    }

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        sb.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");

        for (var i = 1; i <= sheetCount; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");

        sb.Append("</Types>");
        return sb.ToString();
    }

    private const string RootRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Workbook(XlsxSheet[] sheets)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" """);
        sb.Append("""xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");

        for (var i = 0; i < sheets.Length; i++)
        {
            // Excel не принимает в названии листа : \ / ? * [ ] и больше 31 символа.
            var name = SecurityElement.Escape(SanitizeSheetName(sheets[i].Name));
            sb.Append($"""<sheet name="{name}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }

        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string SanitizeSheetName(string name)
    {
        var cleaned = new string(name.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Лист";

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string WorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");

        for (var i = 1; i <= sheetCount; i++)
        {
            sb.Append($"""<Relationship Id="rId{i}" """);
            sb.Append("""Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" """);
            sb.Append($"""Target="worksheets/sheet{i}.xml"/>""");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }
}
