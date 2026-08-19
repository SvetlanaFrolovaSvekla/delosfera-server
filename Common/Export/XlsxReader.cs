using System.IO.Compression;
using System.Xml.Linq;

namespace delosfera_server.Common.Export;

/// <summary>
/// Чтение книги Office Open XML без сторонних библиотек.
///
/// Нужен один сценарий — забрать плоскую таблицу из файла, который сформировал
/// Отдел методологии в Excel. Ради него библиотека с отдельным согласованием в
/// банке не оправдана, а разбор пары XML внутри zip умещается в этот файл.
///
/// Excel по умолчанию пишет текст не в ячейку, а в общий словарь строк
/// (sharedStrings.xml), и ячейка ссылается на него номером. Читатель, который об
/// этом не знает, получает вместо наименований ВНД колонку чисел.
/// </summary>
public static class XlsxReader
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>
    /// Строки первого листа. Каждая строка — массив по колонкам A, B, C…;
    /// пропущенные ячейки заполняются пустыми строками, чтобы номер колонки
    /// совпадал с позицией в массиве.
    /// </summary>
    public static List<string[]> ReadFirstSheet(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var shared = ReadSharedStrings(zip);
        var sheetEntry = FindFirstSheet(zip)
            ?? throw new InvalidOperationException("В файле не найден лист с данными");

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);

        var rows = new List<string[]>();

        foreach (var row in doc.Descendants(Main + "row"))
        {
            var cells = new List<string>();

            foreach (var cell in row.Elements(Main + "c"))
            {
                // Пропуски в разметке — это пустые ячейки: Excel их не пишет вовсе,
                // и без выравнивания колонки поехали бы влево.
                var index = ColumnIndex((string?) cell.Attribute("r"));
                while (cells.Count < index) cells.Add(string.Empty);

                cells.Add(CellValue(cell, shared));
            }

            rows.Add(cells.ToArray());
        }

        return rows;
    }

    private static ZipArchiveEntry? FindFirstSheet(ZipArchive zip) =>
        zip.GetEntry("xl/worksheets/sheet1.xml")
        ?? zip.Entries.FirstOrDefault(e =>
            e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        return doc.Descendants(Main + "si")
            // Внутри одной строки бывает несколько фрагментов с разным оформлением —
            // склеиваем, иначе текст обрежется на первом изменении шрифта.
            .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
            .ToList();
    }

    private static string CellValue(XElement cell, List<string> shared)
    {
        var type = (string?) cell.Attribute("t");

        if (type == "inlineStr")
            return string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)).Trim();

        var value = cell.Element(Main + "v")?.Value;
        if (value is null) return string.Empty;

        if (type == "s")
            return int.TryParse(value, out var index) && index >= 0 && index < shared.Count
                ? shared[index].Trim()
                : string.Empty;

        return value.Trim();
    }

    /// <summary>Номер колонки из ссылки вида «C7» — буквенная часть, начиная с нуля.</summary>
    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return 0;

        var index = 0;

        foreach (var c in reference)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return Math.Max(0, index - 1);
    }

    /// <summary>
    /// Дата из ячейки. Excel хранит даты числом дней от 30.12.1899, но выгрузки
    /// из внешних систем часто приходят строкой — принимаем оба вида.
    /// </summary>
    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim();

        if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var serial) && serial > 0)
        {
            return DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays(serial));
        }

        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d.M.yy"];

        foreach (var format in formats)
        {
            if (DateOnly.TryParseExact(text, format,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
                return date;
        }

        return DateOnly.TryParse(text, out var parsed) ? parsed : null;
    }
}
