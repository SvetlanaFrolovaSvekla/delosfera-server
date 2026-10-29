using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Простой текст .docx — для поиска по тексту редакции (см. VndRedactionSearchText).
///
/// Word режет фразу на произвольные куски (runs) — по правке, по смене шрифта, по проверке
/// орфографии, — поэтому текст собирается по абзацам целиком, а пробелы схлопываются: иначе
/// «срок действия» в тексте и «срок  действия» в запросе не совпали бы.
/// </summary>
public static class DocxPlainTextExtractor
{
    /// <summary>Потолок на одну редакцию: искать по многомегабайтным выгрузкам незачем.</summary>
    public const int MaxLength = 2_000_000;

    /// <summary>Не бросает: битый или не-docx файл (например, старый .doc) даёт пустую строку.</summary>
    public static string Extract(Stream docxStream)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(docxStream, false);
            var main = doc.MainDocumentPart;
            if (main?.Document?.Body is null) return string.Empty;

            var sb = new StringBuilder();
            AppendParagraphs(sb, main.Document.Body);
            if (main.FootnotesPart?.Footnotes is { } footnotes) AppendParagraphs(sb, footnotes);
            if (main.EndnotesPart?.Endnotes is { } endnotes) AppendParagraphs(sb, endnotes);

            var text = sb.ToString();
            return text.Length > MaxLength ? text[..MaxLength] : text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AppendParagraphs(StringBuilder sb, DocumentFormat.OpenXml.OpenXmlElement root)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            // Абзацы внутри надписей (text box) вложены в другой абзац — их текст уже попадёт
            // в самый внешний, второй раз не берём.
            if (paragraph.Ancestors<Paragraph>().Any()) continue;

            var line = new StringBuilder();
            foreach (var el in paragraph.Descendants())
            {
                switch (el)
                {
                    case Text t: line.Append(t.Text); break;
                    case TabChar or Break or CarriageReturn: line.Append(' '); break;
                    case NoBreakHyphen: line.Append('-'); break;
                }
            }

            var normalized = Normalize(line.ToString());
            if (normalized.Length == 0) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(normalized);
            if (sb.Length > MaxLength) return;
        }
    }

    /// <summary>Схлопнуть любые пробельные символы (включая неразрывный пробел) в один пробел.
    /// Тем же способом нормализуется и строка поиска (см. VndService.SearchAsync).</summary>
    public static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || ch == ' ' || ch == ' ')
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
