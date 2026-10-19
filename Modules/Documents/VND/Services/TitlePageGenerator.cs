using System.Globalization;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Одна строка блока «Внесены изменения, дополнения»: решение (протокол/СЗ/приказ) и дата
/// его вступления в силу.</summary>
public sealed record TitlePageDecision(DateOnly? Date, string? Code, DateOnly? EffectiveDate);

/// <summary>Данные одной колонки Титульного листа («Текущая редакция» или «Предыдущая редакция»).
/// Null вместо колонки — колонка пустая (у первой редакции нет предыдущей).</summary>
public sealed record TitlePageColumn(
    string Title,
    string? Organ,
    DateOnly? AdoptionDate,
    string? AdoptionCode,
    DateOnly? EffectiveDate,
    string? Developer,
    string? Code,
    IReadOnlyList<string> ResponsibleExecutors,
    IReadOnlyList<TitlePageDecision> Decisions,
    string? DecisionsDeveloper,
    DateOnly? CancelDecisionDate,
    string? CancelDecisionCode,
    DateOnly? CancelDate);

/// <summary>Всё, что нужно для заполнения Титульного листа.</summary>
/// <param name="VndTitle">Название ВНД — шапка над таблицей.</param>
/// <param name="ChangesLinkUrl">Ссылка с заголовка «Внесены изменения, дополнения» (на ТИД редакции в
/// системе); null — заголовок остаётся обычным текстом.</param>
public sealed record TitlePageData(
    string VndTitle,
    TitlePageColumn Current,
    TitlePageColumn? Previous,
    string? ChangesLinkUrl);

public interface ITitlePageGenerator
{
    byte[] Generate(TitlePageData data);
}

/// <summary>Генератор Титульного листа редакции ВНД по встроенному шаблону TitlePageTemplate.docx
/// (шаблон банка: логотип, название ВНД, таблица «Реквизиты / Текущая редакция / Предыдущая
/// редакция», блоки «Внесены изменения, дополнения» и «Отменено действие»).
///
/// Строки шаблона находятся по тексту первой ячейки, а не по номеру — так шаблон можно править
/// (добавить отступ, поменять оформление), не трогая код. Одинаковые подписи («Дата вступления в
/// силу», «Разработчик», «Дата и № протокола утверждения») различаются по блоку, в котором стоят:
/// блок начинается строкой-заголовком на всю ширину таблицы.</summary>
public class TitlePageGenerator : ITitlePageGenerator
{
    // LogicalName встроенного ресурса — см. EmbeddedResource в delosfera-server.csproj.
    private const string TemplateResourceName = "TitlePageTemplate.docx";

    private const string Font = "Arial";
    private const string LinkColor = "0563C1";

    private enum Block { Requisites, Changes, Cancel }

    public byte[] Generate(TitlePageData data)
    {
        using var templateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Встроенный ресурс {TemplateResourceName} не найден");

        using var output = new MemoryStream();
        templateStream.CopyTo(output);
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, true))
        {
            var mainPart = doc.MainDocumentPart!;
            var table = mainPart.Document.Body!.Descendants<Table>().FirstOrDefault()
                        ?? throw new InvalidOperationException("В шаблоне Титульного листа нет таблицы");

            var block = Block.Requisites;
            foreach (var row in table.Elements<TableRow>().ToList())
            {
                var cells = row.Elements<TableCell>().ToList();
                var label = Normalize(CellText(cells.FirstOrDefault()));

                // Строка на всю ширину таблицы: логотип, название ВНД или заголовок блока.
                if (cells.Count == 1)
                {
                    if (label == "названиевнд")
                        SetLines(cells[0], [new Line(data.VndTitle.ToUpper(new CultureInfo("ru-RU")), Bold: true)]);
                    else if (label.StartsWith("внесеныизменения", StringComparison.Ordinal))
                    {
                        block = Block.Changes;
                        if (!string.IsNullOrWhiteSpace(data.ChangesLinkUrl))
                            MakeLink(mainPart, cells[0], data.ChangesLinkUrl!);
                    }
                    else if (label.StartsWith("отменено", StringComparison.Ordinal))
                        block = Block.Cancel;
                    continue;
                }

                if (cells.Count < 3) continue;

                var current = Values(block, label, data.Current);
                var previous = data.Previous is null ? null : Values(block, label, data.Previous);
                if (current is null) continue; // шапка "Реквизиты / Текущая / Предыдущая" и прочее

                SetLines(cells[1], current);
                SetLines(cells[2], previous ?? []);
            }

            mainPart.Document.Save();
        }

        return output.ToArray();
    }

    /// <summary>Значение строки шаблона для колонки. Null — строка не заполняемая (не наша подпись).</summary>
    private static List<Line>? Values(Block block, string label, TitlePageColumn c)
    {
        switch (block)
        {
            case Block.Requisites:
                if (label.StartsWith("наименование", StringComparison.Ordinal)) return One(c.Title);
                if (label.StartsWith("органутверждения", StringComparison.Ordinal)) return One(c.Organ, bold: true);
                if (label.StartsWith("датаи№протокола", StringComparison.Ordinal)) return One(DateAndCode(c.AdoptionDate, c.AdoptionCode));
                if (label.StartsWith("датавступления", StringComparison.Ordinal)) return One(FormatDate(c.EffectiveDate));
                if (label.StartsWith("разработчик", StringComparison.Ordinal)) return One(c.Developer);
                if (label.StartsWith("коддокумента", StringComparison.Ordinal)) return One(c.Code);
                if (label.StartsWith("ответственные", StringComparison.Ordinal))
                    return One(c.ResponsibleExecutors.Count > 0 ? string.Join(", ", c.ResponsibleExecutors) : null);
                return null;

            case Block.Changes:
                if (label.StartsWith("датаи№решения", StringComparison.Ordinal))
                    return c.Decisions
                        .Select(d => DateAndCode(d.Date, d.Code))
                        .Where(s => s is not null)
                        .Select(s => new Line(s + ";"))
                        .ToList();
                if (label.StartsWith("датавступления", StringComparison.Ordinal))
                    return c.Decisions
                        .Where(d => d.Date is not null || !string.IsNullOrWhiteSpace(d.Code))
                        .Select(d => new Line((FormatDate(d.EffectiveDate) ?? "—") + ";"))
                        .ToList();
                if (label.StartsWith("разработчик", StringComparison.Ordinal)) return One(c.DecisionsDeveloper);
                return null;

            case Block.Cancel:
                if (label.StartsWith("датаи№протокола", StringComparison.Ordinal))
                    return One(DateAndCode(c.CancelDecisionDate, c.CancelDecisionCode));
                if (label.StartsWith("датаотмены", StringComparison.Ordinal)) return One(FormatDate(c.CancelDate));
                return null;
        }

        return null;
    }

    private static List<Line> One(string? text, bool bold = false) =>
        string.IsNullOrWhiteSpace(text) ? [] : [new Line(text.Trim(), Bold: bold)];

    /// <summary>"01.01.2020г." — формат дат шаблона банка.</summary>
    internal static string? FormatDate(DateOnly? date) =>
        date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + (date is null ? null : "г.");

    /// <summary>"01.01.2020г. №12(2)"; если чего-то нет — только то, что есть.</summary>
    internal static string? DateAndCode(DateOnly? date, string? code)
    {
        var parts = new List<string>();
        if (FormatDate(date) is { } d) parts.Add(d);
        if (!string.IsNullOrWhiteSpace(code)) parts.Add("№" + code.Trim().TrimStart('№').Trim());
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    private static string CellText(TableCell? cell) =>
        cell is null ? "" : string.Concat(cell.Descendants<Text>().Select(t => t.Text));

    /// <summary>Подпись строки без пробелов/переносов в нижнем регистре: "Разработчик\n(ФИО, должность)"
    /// → "разработчик(фио,должность)".</summary>
    private static string Normalize(string text) =>
        new string(text.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToLower(new CultureInfo("ru-RU"));

    private sealed record Line(string Text, bool Bold = false);

    /// <summary>Заменяет содержимое ячейки строками (каждая — отдельный абзац) по центру, тем же
    /// шрифтом Arial, что и весь шаблон; свойства ячейки (границы, ширина) не трогаются.</summary>
    private static void SetLines(TableCell cell, IReadOnlyList<Line> lines)
    {
        foreach (var element in cell.Elements().Where(e => e is not TableCellProperties).ToList())
            element.Remove();

        if (lines.Count == 0)
        {
            cell.AppendChild(MakeParagraph(null, last: true)); // ячейка Word обязана содержать абзац
            return;
        }

        for (var i = 0; i < lines.Count; i++)
            cell.AppendChild(MakeParagraph(lines[i], last: i == lines.Count - 1));
    }

    private static Paragraph MakeParagraph(Line? line, bool last)
    {
        var props = new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = last ? "0" : "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = JustificationValues.Center });
        var paragraph = new Paragraph(props);
        if (line is not null) paragraph.AppendChild(MakeRun(line.Text, line.Bold));
        return paragraph;
    }

    private static Run MakeRun(string text, bool bold, bool link = false)
    {
        var runProps = new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font });
        if (bold) runProps.Append(new Bold(), new BoldComplexScript());
        if (link) runProps.Append(new Color { Val = LinkColor }, new Underline { Val = UnderlineValues.Single });
        return new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    /// <summary>Делает текст заголовка блока гиперссылкой (на ТИД редакции в системе).</summary>
    private static void MakeLink(MainDocumentPart part, TableCell cell, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var text = CellText(cell).Trim();
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null || text.Length == 0) return;

        var relationship = part.AddHyperlinkRelationship(uri, true);
        foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();
        paragraph.AppendChild(new Hyperlink(MakeRun(text, bold: true, link: true))
        {
            Id = relationship.Id,
            History = OnOffValue.FromBoolean(true),
        });
    }
}
