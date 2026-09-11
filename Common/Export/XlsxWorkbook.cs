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

    /// <summary>Обвести все ячейки (шапку и данные) тонкой рамкой со всех сторон.</summary>
    public bool AllBorders { get; set; }

    /// <summary>Цвет заливки шапки (RRGGBB, без #). Не задан — заливки нет.</summary>
    public string? HeaderFillHex { get; set; }

    /// <summary>
    /// Цвет текста ячейки данных (RRGGBB, без #) по (индекс строки данных с 0, индекс колонки с 0).
    /// Возвращает null — цвет текста по умолчанию (чёрный).
    /// </summary>
    public Func<int, int, string?>? CellFontColor { get; set; }
}

/// <summary>
/// Сборка книги Office Open XML без сторонних библиотек.
///
/// Выгрузки банка — плоские таблицы без формул, и ради них тянуть в сборку зависимость
/// с отдельным согласованием незачем. Строки пишутся inline-строками, поэтому таблица
/// общих строк не нужна. Стилизация (рамки, заливка шапки, цвет текста) — минимальный
/// набор из xl/styles.xml: реестр стилей собирает только реально использованные
/// сочетания шрифт/заливка/рамка, а лист без единого запроса стиля (все три свойства
/// XlsxSheet не заданы) выдаёт байт-в-байт тот же XML, что и раньше — xl/styles.xml
/// в этом случае вообще не создаётся.
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

        var styles = new StyleRegistry();
        var sheetXml = new string[sheets.Length];

        // Листы собираются до записи архива — по пути они регистрируют нужные стили,
        // и только тогда становится известно, понадобится ли xl/styles.xml вообще.
        for (var i = 0; i < sheets.Length; i++)
            sheetXml[i] = Sheet(sheets[i], styles);

        var hasStyles = styles.HasCustomStyles;

        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes(sheets.Length, hasStyles));
            Write(zip, "_rels/.rels", RootRels);
            Write(zip, "xl/workbook.xml", Workbook(sheets));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Length, hasStyles));

            if (hasStyles)
                Write(zip, "xl/styles.xml", styles.BuildStylesXml());

            for (var i = 0; i < sheets.Length; i++)
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", sheetXml[i]);
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Sheet(XlsxSheet sheet, StyleRegistry styles)
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

        // Шапка: заливка (если задана) + белый жирный текст для читаемости на тёмном фоне,
        // плюс рамка, если она запрошена для всей таблицы.
        var headerNeedsStyle = sheet.HeaderFillHex is not null || sheet.AllBorders;
        int? headerStyle = headerNeedsStyle
            ? styles.GetStyle(
                bold: sheet.HeaderFillHex is not null,
                fontColorHex: sheet.HeaderFillHex is not null ? "FFFFFF" : null,
                fillColorHex: sheet.HeaderFillHex,
                border: sheet.AllBorders)
            : null;

        AppendRow(sb, 1, sheet.Header, headerStyle is null ? null : _ => headerStyle);

        var needsRowStyles = sheet.CellFontColor is not null || sheet.AllBorders;

        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var dataRowIndex = i;

            Func<int, int?>? rowStyleFn = needsRowStyles
                ? col =>
                {
                    var fontColor = sheet.CellFontColor?.Invoke(dataRowIndex, col);
                    if (fontColor is null && !sheet.AllBorders) return null;

                    return styles.GetStyle(bold: false, fontColorHex: fontColor, fillColorHex: null, border: sheet.AllBorders);
                }
                : null;

            AppendRow(sb, i + 2, sheet.Rows[i], rowStyleFn);
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, string[] values, Func<int, int?>? styleForColumn)
    {
        sb.Append($"""<row r="{rowIndex}">""");

        for (var c = 0; c < values.Length; c++)
        {
            var style = styleForColumn?.Invoke(c);
            var styleAttr = style is not null ? $" s=\"{style}\"" : "";

            sb.Append($"""<c r="{Column(c)}{rowIndex}"{styleAttr} t="inlineStr"><is><t xml:space="preserve">""");
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

    private static string ContentTypes(int sheetCount, bool hasStyles)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        sb.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");

        if (hasStyles)
            sb.Append("""<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");

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

    private static string WorkbookRels(int sheetCount, bool hasStyles)
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

        if (hasStyles)
        {
            sb.Append($"""<Relationship Id="rId{sheetCount + 1}" """);
            sb.Append("""Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" """);
            sb.Append("""Target="styles.xml"/>""");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    /// <summary>
    /// Реестр стилей одной книги: дедуплицирует шрифты/заливки/рамки и собирает из них
    /// xl/styles.xml. Индекс 0 в cellXfs всегда обычная ячейка без стиля — под него листы
    /// без единого запроса стиля просто не создают записей (HasCustomStyles остаётся false),
    /// и BuildStylesXml() в этом случае не вызывается вовсе.
    /// </summary>
    private sealed class StyleRegistry
    {
        // Индекс 0 — шрифт по умолчанию (не жирный, цвет по умолчанию).
        private readonly List<(bool Bold, string? ColorHex)> _fonts = [(false, null)];

        // Индексы 0 и 1 — обязательные по формату OOXML заглушки (none / gray125),
        // реальные заливки добавляются начиная с индекса 2.
        private readonly List<string?> _fills = [null, null];

        // Индекс 0 — без рамки.
        private readonly List<bool> _borders = [false];

        private readonly Dictionary<(int Font, int Fill, int Border), int> _cellXfs =
            new() { [(0, 0, 0)] = 0 };

        public bool HasCustomStyles => _cellXfs.Count > 1;

        public int GetStyle(bool bold, string? fontColorHex, string? fillColorHex, bool border)
        {
            var fontIndex = GetFont(bold, fontColorHex);
            var fillIndex = fillColorHex is null ? 0 : GetFill(fillColorHex);
            var borderIndex = GetBorder(border);

            var key = (fontIndex, fillIndex, borderIndex);
            if (_cellXfs.TryGetValue(key, out var existing))
                return existing;

            var index = _cellXfs.Count;
            _cellXfs[key] = index;
            return index;
        }

        private int GetFont(bool bold, string? colorHex)
        {
            for (var i = 0; i < _fonts.Count; i++)
                if (_fonts[i] == (bold, colorHex))
                    return i;

            _fonts.Add((bold, colorHex));
            return _fonts.Count - 1;
        }

        private int GetFill(string colorHex)
        {
            for (var i = 2; i < _fills.Count; i++)
                if (_fills[i] == colorHex)
                    return i;

            _fills.Add(colorHex);
            return _fills.Count - 1;
        }

        private int GetBorder(bool thin)
        {
            if (!thin) return 0;

            for (var i = 1; i < _borders.Count; i++)
                if (_borders[i]) return i;

            _borders.Add(true);
            return _borders.Count - 1;
        }

        public string BuildStylesXml()
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sb.Append("""<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

            sb.Append($"""<fonts count="{_fonts.Count}">""");
            foreach (var (bold, color) in _fonts)
            {
                sb.Append("<font>");
                sb.Append("""<sz val="11"/><name val="Calibri"/>""");
                if (bold) sb.Append("<b/>");
                if (color is not null) sb.Append($"""<color rgb="FF{color}"/>""");
                sb.Append("</font>");
            }
            sb.Append("</fonts>");

            sb.Append($"""<fills count="{_fills.Count}">""");
            sb.Append("""<fill><patternFill patternType="none"/></fill>""");
            sb.Append("""<fill><patternFill patternType="gray125"/></fill>""");
            for (var i = 2; i < _fills.Count; i++)
                sb.Append($"""<fill><patternFill patternType="solid"><fgColor rgb="FF{_fills[i]}"/><bgColor indexed="64"/></patternFill></fill>""");
            sb.Append("</fills>");

            sb.Append($"""<borders count="{_borders.Count}">""");
            sb.Append("<border><left/><right/><top/><bottom/><diagonal/></border>");
            for (var i = 1; i < _borders.Count; i++)
            {
                sb.Append("<border>");
                sb.Append("""<left style="thin"><color indexed="64"/></left>""");
                sb.Append("""<right style="thin"><color indexed="64"/></right>""");
                sb.Append("""<top style="thin"><color indexed="64"/></top>""");
                sb.Append("""<bottom style="thin"><color indexed="64"/></bottom>""");
                sb.Append("<diagonal/>");
                sb.Append("</border>");
            }
            sb.Append("</borders>");

            sb.Append("""<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>""");

            sb.Append($"""<cellXfs count="{_cellXfs.Count}">""");
            foreach (var key in _cellXfs.OrderBy(kv => kv.Value).Select(kv => kv.Key))
            {
                var (font, fill, border) = key;
                sb.Append("<xf numFmtId=\"0\" fontId=\"").Append(font)
                    .Append("\" fillId=\"").Append(fill)
                    .Append("\" borderId=\"").Append(border)
                    .Append("\" xfId=\"0\"");

                if (font != 0) sb.Append(" applyFont=\"1\"");
                if (fill != 0) sb.Append(" applyFill=\"1\"");
                if (border != 0) sb.Append(" applyBorder=\"1\"");

                sb.Append("/>");
            }
            sb.Append("</cellXfs>");

            sb.Append("""<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>""");

            sb.Append("</styleSheet>");
            return sb.ToString();
        }
    }
}
