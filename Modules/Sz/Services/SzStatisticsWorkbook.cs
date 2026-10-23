using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>
/// Оформленная книга Excel для выгрузки аналитики служебных записок.
///
/// Общий <see cref="delosfera_server.Common.Export.XlsxWorkbook"/> пишет плоские таблицы
/// строками: числа в нём — текст (не суммируются, Excel ругается зелёными уголками), нет
/// заголовка с периодом и фильтрами, итоговой строки, закреплённой шапки и автофильтра.
/// Для отчёта, который уходит руководству, этого мало, поэтому здесь своя, тоже
/// безбиблиотечная, сборка OOXML с фиксированным набором стилей.
/// </summary>
internal static class SzStatisticsWorkbook
{
    // ── стили (индексы в cellXfs, см. StylesXml) ────────────────────────────
    public const int SDefault = 0;
    public const int STitle = 1;
    public const int SSubtitle = 2;
    public const int SHeader = 3;
    public const int SText = 4;
    public const int STextWrap = 5;
    public const int SInt = 6;
    public const int SPercent = 7;
    public const int SDate = 8;
    public const int STotalLabel = 9;
    public const int STotalInt = 10;
    public const int STotalPercent = 11;
    public const int SIntOverdue = 12;
    public const int SStateOverdue = 13;
    public const int SStateExecuted = 14;
    public const int SStateInWork = 15;
    public const int SStateOther = 16;
    public const int SKpiLabel = 17;
    public const int SIntMuted = 18;
    public const int SFilterLabel = 19;
    public const int SCenter = 20;

    public readonly record struct Cell(object? Value, int Style)
    {
        public static Cell Text(string? v, int style = SText) => new(v ?? "", style);
        public static Cell Num(double v, int style = SInt) => new(v, style);
        public static Cell Date(DateOnly? v) => v is null ? new Cell("—", SCenter) : new Cell(v.Value, SDate);
        public static readonly Cell Empty = new(null, SDefault);
    }

    public sealed class Sheet
    {
        public required string Name { get; init; }
        public required double[] Widths { get; init; }
        public List<Cell[]> Rows { get; } = [];
        public Dictionary<int, double> RowHeights { get; } = [];
        public List<string> Merges { get; } = [];

        /// <summary>Сколько верхних строк закрепить (шапка таблицы остаётся видна при прокрутке).</summary>
        public int FreezeRows { get; set; }

        /// <summary>Диапазон автофильтра, например "A4:H20".</summary>
        public string? AutoFilter { get; set; }

        public int Add(params Cell[] cells)
        {
            Rows.Add(cells);
            return Rows.Count; // номер строки в Excel (с 1)
        }

        public void AddBlank() => Rows.Add([]);
    }

    public static string Col(int index)
    {
        var name = string.Empty;
        for (var i = index; i >= 0; i = i / 26 - 1)
            name = (char)('A' + i % 26) + name;
        return name;
    }

    public static byte[] Build(params Sheet[] sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes(sheets.Length));
            Write(zip, "_rels/.rels", RootRels);
            Write(zip, "docProps/core.xml", CoreProps());
            Write(zip, "xl/workbook.xml", Workbook(sheets));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Length));
            Write(zip, "xl/styles.xml", StylesXml);
            for (var i = 0; i < sheets.Length; i++)
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i], i == 0));
        }
        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Esc(string s) => SecurityElement.Escape(s) ?? string.Empty;

    private static string SheetXml(Sheet sheet, bool selected)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("""<sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>""");

        sb.Append("<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"");
        if (selected) sb.Append(" tabSelected=\"1\"");
        sb.Append('>');
        if (sheet.FreezeRows > 0)
        {
            var topLeft = $"A{sheet.FreezeRows + 1}";
            sb.Append($"""<pane ySplit="{sheet.FreezeRows}" topLeftCell="{topLeft}" activePane="bottomLeft" state="frozen"/>""");
            sb.Append($"""<selection pane="bottomLeft" activeCell="{topLeft}" sqref="{topLeft}"/>""");
        }
        sb.Append("</sheetView></sheetViews>");
        sb.Append("""<sheetFormatPr defaultRowHeight="15"/>""");

        sb.Append("<cols>");
        for (var i = 0; i < sheet.Widths.Length; i++)
            sb.Append($"""<col min="{i + 1}" max="{i + 1}" width="{sheet.Widths[i].ToString(CultureInfo.InvariantCulture)}" customWidth="1"/>""");
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var rowNum = r + 1;
            sb.Append($"<row r=\"{rowNum}\"");
            if (sheet.RowHeights.TryGetValue(rowNum, out var h))
                sb.Append($" ht=\"{h.ToString(CultureInfo.InvariantCulture)}\" customHeight=\"1\"");
            sb.Append('>');

            var cells = sheet.Rows[r];
            for (var c = 0; c < cells.Length; c++)
            {
                var cell = cells[c];
                var reference = $"{Col(c)}{rowNum}";
                var style = cell.Style != SDefault ? $" s=\"{cell.Style}\"" : "";

                switch (cell.Value)
                {
                    case null:
                        if (cell.Style != SDefault) sb.Append($"<c r=\"{reference}\"{style}/>");
                        break;
                    case DateOnly d:
                        var serial = d.DayNumber - new DateOnly(1899, 12, 30).DayNumber;
                        sb.Append($"<c r=\"{reference}\"{style}><v>{serial}</v></c>");
                        break;
                    case int or long or double or decimal or float:
                        var num = Convert.ToDouble(cell.Value, CultureInfo.InvariantCulture);
                        sb.Append($"<c r=\"{reference}\"{style}><v>{num.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        sb.Append($"<c r=\"{reference}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">");
                        sb.Append(Esc(cell.Value.ToString() ?? ""));
                        sb.Append("</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");

        if (sheet.AutoFilter is not null)
            sb.Append($"<autoFilter ref=\"{sheet.AutoFilter}\"/>");

        if (sheet.Merges.Count > 0)
        {
            sb.Append($"<mergeCells count=\"{sheet.Merges.Count}\">");
            foreach (var m in sheet.Merges) sb.Append($"<mergeCell ref=\"{m}\"/>");
            sb.Append("</mergeCells>");
        }

        // Печать: альбомная, по ширине страницы.
        sb.Append("""<pageMargins left="0.4" right="0.4" top="0.5" bottom="0.5" header="0.3" footer="0.3"/>""");
        sb.Append("""<pageSetup paperSize="9" orientation="landscape" fitToWidth="1" fitToHeight="0"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        sb.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        sb.Append("""<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        sb.Append("""<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>""");
        for (var i = 1; i <= sheetCount; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private const string RootRels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>""";

    private static string CoreProps()
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
               "<dc:title>Аналитика служебных записок</dc:title><dc:creator>Делосфера</dc:creator>" +
               $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:created>" +
               "</cp:coreProperties>";
    }

    private static string Workbook(Sheet[] sheets)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("<bookViews><workbookView activeTab=\"0\"/></bookViews><sheets>");
        for (var i = 0; i < sheets.Length; i++)
            sb.Append($"""<sheet name="{Esc(SheetName(sheets[i].Name))}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        sb.Append("</sheets>");

        // Автофильтру Excel нужно скрытое имя _FilterDatabase на каждом листе с фильтром.
        var names = new StringBuilder();
        for (var i = 0; i < sheets.Length; i++)
        {
            if (sheets[i].AutoFilter is not { } range) continue;
            var parts = range.Split(':');
            var abs = string.Join(':', parts.Select(Absolute));
            names.Append($"""<definedName name="_xlnm._FilterDatabase" localSheetId="{i}" hidden="1">'{Esc(SheetName(sheets[i].Name).Replace("'", "''"))}'!{abs}</definedName>""");
        }
        if (names.Length > 0) sb.Append("<definedNames>").Append(names).Append("</definedNames>");

        sb.Append("</workbook>");
        return sb.ToString();
    }

    private static string Absolute(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
        return $"${letters}${cellRef[letters.Length..]}";
    }

    private static string SheetName(string name)
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
            sb.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        sb.Append($"""<Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // Порядок xf в cellXfs строго соответствует константам S* выше.
    // Шрифты: 0 обычный, 1 заголовок, 2 подзаголовок, 3 шапка, 4 жирный, 5 красный жирный,
    //         6 зелёный, 7 синий, 8 серый, 9 красный.
    // Заливки: 2 шапка (синий), 3 итог (светло-серый), 4 просрочка (розовый), 5 KPI (светлый).
    // Рамки: 1 тонкая светлая.
    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="2">
            <numFmt numFmtId="164" formatCode="0.0%"/>
            <numFmt numFmtId="165" formatCode="dd.mm.yyyy"/>
          </numFmts>
          <fonts count="10">
            <font><sz val="10"/><color rgb="FF1C2740"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="15"/><color rgb="FF0F1B2D"/><name val="Calibri"/><family val="2"/></font>
            <font><sz val="10"/><color rgb="FF6B7A90"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="10"/><color rgb="FF1C2740"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="10"/><color rgb="FFC0392B"/><name val="Calibri"/><family val="2"/></font>
            <font><sz val="10"/><color rgb="FF1C7A4D"/><name val="Calibri"/><family val="2"/></font>
            <font><sz val="10"/><color rgb="FF2F68F5"/><name val="Calibri"/><family val="2"/></font>
            <font><sz val="10"/><color rgb="FF8B97AB"/><name val="Calibri"/><family val="2"/></font>
            <font><sz val="10"/><color rgb="FFC0392B"/><name val="Calibri"/><family val="2"/></font>
          </fonts>
          <fills count="6">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2F68F5"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFEEF2F7"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFFBEAE7"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF6F8FB"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left style="thin"><color rgb="FFD5DCE6"/></left><right style="thin"><color rgb="FFD5DCE6"/></right><top style="thin"><color rgb="FFD5DCE6"/></top><bottom style="thin"><color rgb="FFD5DCE6"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="21">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="3" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="top"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>
            <xf numFmtId="3" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="top"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="top"/></xf>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
            <xf numFmtId="0" fontId="4" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
            <xf numFmtId="3" fontId="4" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right"/></xf>
            <xf numFmtId="164" fontId="4" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right"/></xf>
            <xf numFmtId="3" fontId="5" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="top"/></xf>
            <xf numFmtId="0" fontId="9" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
            <xf numFmtId="0" fontId="6" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
            <xf numFmtId="0" fontId="7" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
            <xf numFmtId="0" fontId="8" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
            <xf numFmtId="0" fontId="4" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="3" fontId="8" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="right" vertical="top"/></xf>
            <xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="top"/></xf>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
