using System.Text.RegularExpressions;
using Ganss.Xss;

namespace delosfera_server.Common.Services;

public interface IDocumentHtmlService
{
    /// <summary>Очистить разметку, пришедшую от пользователя.</summary>
    string? Sanitize(string? html);

    /// <summary>Текст без разметки — для поиска и коротких выдержек в списках.</summary>
    string? ToPlainText(string? html);
}

/// <summary>
/// Разметка текста документа (SZ-02).
///
/// Текст записки набирают в редакторе, а редактор отдаёт HTML. Пришедший от
/// пользователя HTML нельзя ни хранить, ни показывать как есть: он открывается
/// в браузере другого сотрудника, и любой скрипт внутри выполнится с его правами.
/// Поэтому разметка проходит через белый список — всё, чего в нём нет, вырезается.
///
/// Список нарочно узкий: он повторяет то, что умеет редактор, и ни на тег больше.
/// Расширять его нужно вместе с редактором, а не «на всякий случай» — каждый
/// лишний тег это то, что кто-то однажды вставит вручную.
///
/// Ссылки и картинки не разрешены: ссылка уводит из системы, а картинка тянется
/// с чужого сервера и выдаёт ему, кто и когда открыл документ. Файлы прикладывают
/// вложениями, у них своё хранилище и свой контроль.
/// </summary>
public class DocumentHtmlService : IDocumentHtmlService
{
    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private readonly HtmlSanitizer _sanitizer;

    public DocumentHtmlService()
    {
        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "p", "br", "strong", "b", "em", "i", "u", "s", "span",
                     "h1", "h2", "h3", "ul", "ol", "li", "blockquote",
                     "table", "thead", "tbody", "tr", "th", "td",
                 })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] {"style", "colspan", "rowspan", "colwidth"})
            _sanitizer.AllowedAttributes.Add(attr);

        // Из стилей оставляем только то, что ставит панель редактора: начертание,
        // шрифт, размер, выравнивание. Всё прочее — способ спрятать содержимое
        // или перекрыть чужой текст поверх страницы.
        _sanitizer.AllowedCssProperties.Clear();
        foreach (var css in new[]
                 {
                     "font-family", "font-size", "font-weight", "font-style",
                     "text-align", "text-decoration", "color", "background-color",
                 })
            _sanitizer.AllowedCssProperties.Add(css);

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.KeepChildNodes = true;
    }

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var clean = _sanitizer.Sanitize(html).Trim();

        // Редактор на пустом поле отдаёт пустой абзац. Хранить его — значит
        // считать записку с текстом там, где текста нет.
        return clean is "" or "<p></p>" or "<p><br></p>" ? null : clean;
    }

    public string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Блочные теги заменяем пробелом, иначе «конец абзацаНачало» склеится
        // в одно слово и не найдётся ни по одному из них.
        var withBreaks = Regex.Replace(html, "<(/p|/li|/tr|/h[1-3]|br\\s*/?)>", " ",
            RegexOptions.IgnoreCase);

        var text = Tags.Replace(withBreaks, string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Spaces.Replace(text, " ").Trim();

        return text.Length == 0 ? null : text;
    }
}
