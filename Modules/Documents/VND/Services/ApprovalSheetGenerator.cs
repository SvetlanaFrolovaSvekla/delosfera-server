using System.Globalization;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Одна строка Листа согласования — согласующий, чьё ФИО и должность подставляются
/// в таблицу шаблона (старый формат: у всех "Согласовано" и одна общая дата).</summary>
public record ApprovalSheetApproverInfo(string FullName, string? Position);

/// <summary>Режим Листа согласования: обычный (решения, колонка "Подпись" пустая) или с отметками ЭЦП
/// (в колонке "Подпись" — штамп "Подписано с помощью ЭЦП" с данными подписи).</summary>
public enum ApprovalSheetMode
{
    Plain = 0,
    Signed = 1,
}

/// <summary>Данные штампа ЭЦП для колонки "Подпись" (из журнала VndResolutionSignature).
/// Даты — уже в часовом поясе банка.</summary>
public sealed record ApprovalSheetStamp(
    string? SignerName,
    string? OrganizationName,
    string? CertValidNotAfter,
    string? IssuerName,
    DateTime SignedAtLocal,
    string DocumentHash,
    string SignatureSha256);

/// <summary>Строка Листа согласования с фактическим решением согласующего.</summary>
/// <param name="Decision">"Согласовано", "Согласовано с замечаниями", "Отклонено", "Ожидает решения"…</param>
/// <param name="DecisionDetail">Пояснение мелким шрифтом под решением (фаза, "автоматически").</param>
/// <param name="DecidedAtLocal">Когда принято решение (часовой пояс банка); null — решения ещё нет.</param>
/// <param name="Stamp">Штамп ЭЦП (только в режиме <see cref="ApprovalSheetMode.Signed"/>).</param>
/// <param name="SignatureNote">Текст в колонке "Подпись", когда штампа нет (режим Signed).</param>
public sealed record ApprovalSheetRow(
    string FullName,
    string? Position,
    string Decision,
    string? DecisionDetail,
    DateTime? DecidedAtLocal,
    ApprovalSheetStamp? Stamp,
    string? SignatureNote);

public interface IApprovalSheetGenerator
{
    /// <summary>Старый формат: у всех согласующих "Согласовано" и единая дата окончательного
    /// согласования. Оставлен для совместимости.</summary>
    byte[] Generate(string vndTitle, DateTime approvedAt, IReadOnlyList<ApprovalSheetApproverInfo> approvers,
        string? note = null);

    /// <summary>Лист с фактическими решениями каждого согласующего. В режиме
    /// <see cref="ApprovalSheetMode.Signed"/> колонка "Подпись" расширяется и в ней ставится штамп ЭЦП
    /// (или пояснение, почему подписи нет), под таблицей — пояснение о проверке подписей.</summary>
    byte[] Generate(string vndTitle, IReadOnlyList<ApprovalSheetRow> rows, ApprovalSheetMode mode,
        string? note = null);
}

/// <summary>Генератор Листа согласования по встроенному шаблону (ApprovalSheetTemplate.docx) через
/// DocumentFormat.OpenXml — вызывается и при завершении согласования (в т.ч. из фонового таймаут-джоба,
/// без браузера), и по запросу "показать/скачать лист" (ApprovalSheetService).</summary>
public class ApprovalSheetGenerator : IApprovalSheetGenerator
{
    // LogicalName встроенного ресурса — см. EmbeddedResource в delosfera-server.csproj.
    private const string TemplateResourceName = "ApprovalSheetTemplate.docx";

    // Цвета штампа ЭЦП: тёмно-синий текст и рамка, светлая заливка — как у печати.
    private const string StampColor = "1F3A93";
    private const string StampFill = "F2F5FD";
    private const string MutedColor = "7A869A";

    // Ширины колонок таблицы (№, должность, ФИО, результат, дата, подпись). Обычный режим — как в
    // шаблоне; с ЭЦП колонка "Подпись" шире за счёт должности/ФИО, чтобы штамп помещался.
    // Grid — в twips (сумма = ширине таблицы шаблона), tcW — доля в пятидесятых процента (сумма 5000).
    private static readonly int[] SignedGrid = [693, 3000, 2600, 2150, 1400, 4424];
    private static readonly int[] SignedPct = [243, 1051, 911, 753, 491, 1551];

    public byte[] Generate(string vndTitle, DateTime approvedAt, IReadOnlyList<ApprovalSheetApproverInfo> approvers,
        string? note = null)
    {
        var rows = approvers
            .Select(a => new ApprovalSheetRow(a.FullName, a.Position, "Согласовано", null, approvedAt, null, null))
            .ToList();
        return Generate(vndTitle, rows, ApprovalSheetMode.Plain, note);
    }

    public byte[] Generate(string vndTitle, IReadOnlyList<ApprovalSheetRow> rows, ApprovalSheetMode mode,
        string? note = null)
    {
        using var templateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Встроенный ресурс {TemplateResourceName} не найден");

        using var output = new MemoryStream();
        templateStream.CopyTo(output);
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            var titleParagraph = FillTitle(body, vndTitle);
            if (!string.IsNullOrWhiteSpace(note))
                InsertNote(body, titleParagraph, note);
            var table = FillTable(body, rows, mode);
            if (mode == ApprovalSheetMode.Signed && table is not null)
                InsertSignedFooter(table);

            doc.MainDocumentPart.Document.Save();
        }

        return output.ToArray();
    }

    /// <summary>Заголовок шаблона содержит строку вида "К «»" одним текстовым узлом (подтверждено
    /// при разборе шаблона) — вставляем название ВНД между кавычками.</summary>
    private static Paragraph? FillTitle(Body body, string vndTitle)
    {
        var target = body.Descendants<Text>().FirstOrDefault(t => t.Text.Contains("«»"));
        if (target is null) return null;
        target.Text = target.Text.Replace("«»", $"«{vndTitle}»");
        return target.Ancestors<Paragraph>().FirstOrDefault();
    }

    /// <summary>Пояснительная строка сразу под заголовком "К «...»" - по центру, курсивом, тем же
    /// шрифтом Arial, что и весь шаблон. Если заголовок в шаблоне не нашёлся - ставим строку
    /// перед таблицей, чтобы пояснение не потерялось.</summary>
    private static void InsertNote(Body body, Paragraph? titleParagraph, string note)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                    new Italic(),
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "20" }),
                new Text(note) { Space = SpaceProcessingModeValues.Preserve }));

        if (titleParagraph is not null)
        {
            titleParagraph.InsertAfterSelf(paragraph);
            return;
        }

        var table = body.Descendants<Table>().FirstOrDefault();
        if (table is not null) table.InsertBeforeSelf(paragraph);
        else body.AppendChild(paragraph);
    }

    private static Table? FillTable(Body body, IReadOnlyList<ApprovalSheetRow> rows, ApprovalSheetMode mode)
    {
        var table = body.Descendants<Table>().FirstOrDefault();
        if (table is null) return null;

        var templateRows = table.Elements<TableRow>().ToList();
        if (templateRows.Count < 2) return table; // ожидается шапка + хотя бы один пример-ряд

        if (mode == ApprovalSheetMode.Signed)
        {
            SetGrid(table, SignedGrid);
            SetCellWidths(templateRows[0], SignedPct);
            SetCellWidths(templateRows[1], SignedPct);
        }

        // Второй ряд шаблона (первый ряд данных, с примером "1") - образец форматирования ячеек
        // (шрифты, границы, ширины колонок). Остальные пример-ряды шаблона удаляем и заполняем
        // таблицу заново под фактическое число согласующих.
        var templateRow = templateRows[1];
        for (var i = 1; i < templateRows.Count; i++)
            templateRows[i].Remove();

        var order = 1;
        foreach (var item in rows)
        {
            var row = (TableRow)templateRow.CloneNode(true);
            var cells = row.Elements<TableCell>().ToList();

            if (cells.Count >= 6)
            {
                SetCellLines(cells[0], [new Line(order.ToString(CultureInfo.InvariantCulture))]);
                SetCellLines(cells[1], [new Line(item.Position ?? "")]);
                SetCellLines(cells[2], [new Line(item.FullName)]);

                var decisionLines = new List<Line> { new(item.Decision, Bold: item.DecidedAtLocal is not null) };
                if (!string.IsNullOrWhiteSpace(item.DecisionDetail))
                    decisionLines.Add(new Line(item.DecisionDetail!, Size: 16, Color: MutedColor, Italic: true));
                SetCellLines(cells[3], decisionLines);

                SetCellLines(cells[4], item.DecidedAtLocal is { } at
                    ? [new Line(at.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
                       new Line(at.ToString("HH:mm", CultureInfo.InvariantCulture), Size: 16, Color: MutedColor)]
                    : [new Line("—", Color: MutedColor)]);

                if (mode == ApprovalSheetMode.Signed)
                {
                    if (item.Stamp is not null) PutStamp(cells[5], item.Stamp);
                    else SetCellLines(cells[5], [new Line(item.SignatureNote ?? "—", Size: 16, Color: MutedColor, Italic: true)]);
                }
                // Обычный режим: колонка "Подпись" остаётся пустой (лист без подписей).
            }

            table.AppendChild(row);
            order++;
        }

        return table;
    }

    private static void SetGrid(Table table, int[] widths)
    {
        var grid = table.GetFirstChild<TableGrid>();
        if (grid is null) return;
        var cols = grid.Elements<GridColumn>().ToList();
        for (var i = 0; i < cols.Count && i < widths.Length; i++)
            cols[i].Width = widths[i].ToString(CultureInfo.InvariantCulture);
    }

    private static void SetCellWidths(TableRow row, int[] pct)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var i = 0; i < cells.Count && i < pct.Length; i++)
        {
            var props = cells[i].TableCellProperties ??= new TableCellProperties();
            props.TableCellWidth = new TableCellWidth
            {
                Width = pct[i].ToString(CultureInfo.InvariantCulture),
                Type = TableWidthUnitValues.Pct,
            };
        }
    }

    /// <summary>Одна строка текста в ячейке. Size — в половинах пункта (как в OpenXML); null — как в шаблоне.</summary>
    private sealed record Line(
        string Text, bool Bold = false, bool Italic = false, int? Size = null, string? Color = null,
        string? Font = null, JustificationValues? Align = null);

    /// <summary>Заменяет содержимое ячейки строками текста, сохраняя свойства ячейки (границы, ширину)
    /// и свойства первого абзаца (выравнивание шаблона). Каждая строка — отдельный абзац.</summary>
    private static void SetCellLines(TableCell cell, IReadOnlyList<Line> lines)
    {
        var first = cell.Elements<Paragraph>().FirstOrDefault();
        var paragraphProps = first?.ParagraphProperties?.CloneNode(true) as ParagraphProperties;

        foreach (var element in cell.Elements().Where(e => e is not TableCellProperties).ToList())
            element.Remove();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var props = paragraphProps?.CloneNode(true) as ParagraphProperties ?? new ParagraphProperties();
            if (line.Align is { } align) props.Justification = new Justification { Val = align };
            // Строки одной ячейки — без отступа между собой (у стиля Normal шаблона after=160);
            // отступ шаблона остаётся только после последней строки.
            if (i < lines.Count - 1) props.SpacingBetweenLines = new SpacingBetweenLines { After = "0" };
            cell.AppendChild(new Paragraph(props, MakeRun(line)));
        }
    }

    private static Run MakeRun(Line line)
    {
        var font = line.Font ?? "Arial";
        var runProps = new RunProperties(new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font });
        if (line.Bold) runProps.Append(new Bold(), new BoldComplexScript());
        if (line.Italic) runProps.Append(new Italic());
        if (line.Color is not null) runProps.Append(new Color { Val = line.Color });
        if (line.Size is { } size)
            runProps.Append(
                new FontSize { Val = size.ToString(CultureInfo.InvariantCulture) },
                new FontSizeComplexScript { Val = size.ToString(CultureInfo.InvariantCulture) });
        return new Run(runProps, new Text(line.Text) { Space = SpaceProcessingModeValues.Preserve });
    }

    /// <summary>Штамп "Подписано с помощью ЭЦП": вложенная таблица из одной ячейки с двойной синей
    /// рамкой и светлой заливкой — выглядит как оттиск печати. Внутри: владелец, организация, срок
    /// сертификата, издатель, время подписи, хэш подписанного документа и отпечаток самой подписи.</summary>
    private static void PutStamp(TableCell cell, ApprovalSheetStamp stamp)
    {
        foreach (var element in cell.Elements().Where(e => e is not TableCellProperties).ToList())
            element.Remove();

        var borders = new TableBorders(
            new TopBorder { Val = BorderValues.Double, Size = 6, Color = StampColor, Space = 0 },
            new LeftBorder { Val = BorderValues.Double, Size = 6, Color = StampColor, Space = 0 },
            new BottomBorder { Val = BorderValues.Double, Size = 6, Color = StampColor, Space = 0 },
            new RightBorder { Val = BorderValues.Double, Size = 6, Color = StampColor, Space = 0 },
            new InsideHorizontalBorder { Val = BorderValues.None },
            new InsideVerticalBorder { Val = BorderValues.None });

        var inner = new TableCell(new TableCellProperties(
            new TableCellWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = StampFill },
            new TableCellMargin(
                new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "90", Type = TableWidthUnitValues.Dxa })));

        void Add(string text, bool bold = false, int size = 13, string color = StampColor, string? font = null,
            JustificationValues? align = null)
        {
            var props = new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "0", Line = "228", LineRule = LineSpacingRuleValues.Auto });
            if (align is { } a) props.Append(new Justification { Val = a });
            inner.Append(new Paragraph(props, MakeRun(new Line(text, Bold: bold, Size: size, Color: color, Font: font))));
        }

        Add("ПОДПИСАНО С ПОМОЩЬЮ ЭЦП", bold: true, size: 15, align: JustificationValues.Center);
        Add("облачная ЭЦП ГП «Инфоком» · подпись проверена", size: 12, color: MutedColor,
            align: JustificationValues.Center);
        if (!string.IsNullOrWhiteSpace(stamp.SignerName)) Add($"Владелец: {stamp.SignerName}", bold: true);
        if (!string.IsNullOrWhiteSpace(stamp.OrganizationName)) Add($"Организация: {stamp.OrganizationName}");
        if (FormatCertDate(stamp.CertValidNotAfter) is { } certUntil) Add($"Сертификат действителен до: {certUntil}");
        if (!string.IsNullOrWhiteSpace(stamp.IssuerName)) Add($"Издатель: {stamp.IssuerName}");
        Add($"Подписано: {stamp.SignedAtLocal.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}");
        Add("Хэш документа (SHA-256):", size: 12, color: MutedColor);
        foreach (var chunk in Chunk(stamp.DocumentHash, 32))
            Add(chunk, size: 12, font: "Consolas");
        Add($"Отпечаток подписи: {Shorten(stamp.SignatureSha256)}", size: 12, color: MutedColor, font: "Consolas");

        var stampTable = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification { Val = TableRowAlignmentValues.Center },
                borders,
                new TableLayout { Type = TableLayoutValues.Fixed }),
            new TableGrid(new GridColumn { Width = "4200" }),
            new TableRow(inner));

        cell.Append(stampTable);
        // Ячейка Word обязана заканчиваться абзацем.
        cell.Append(new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0" })));
    }

    /// <summary>Пояснение под таблицей в режиме с подписями.</summary>
    private static void InsertSignedFooter(Table table)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "0" }),
            MakeRun(new Line(
                "Подписи ЭЦП поставлены согласующими через облачную ЭЦП ГП «Инфоком» и проверены системой «Делосфера» " +
                "у провайдера при сохранении резолюций. Хэш документа — SHA-256 подписанного содержимого: текста " +
                "резолюции, файлов редакции и кода ВНД. Повторно проверить подпись можно на вкладке «Ход согласования» " +
                "→ «Журнал подписей ЭЦП».",
                Size: 16, Color: MutedColor, Italic: true)));
        table.InsertAfterSelf(paragraph);
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        var text = value.Trim().ToUpperInvariant();
        for (var i = 0; i < text.Length; i += size)
            yield return text.Substring(i, Math.Min(size, text.Length - i));
    }

    private static string Shorten(string hex)
    {
        var text = hex.Trim().ToUpperInvariant();
        return text.Length <= 20 ? text : $"{text[..10]}…{text[^10..]}";
    }

    /// <summary>Срок сертификата приходит от провайдера строкой; если это дата — показываем dd.MM.yyyy,
    /// иначе как есть.</summary>
    private static string? FormatCertDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date)
            ? date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : raw.Trim();
    }
}
