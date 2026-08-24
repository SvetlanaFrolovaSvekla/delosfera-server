namespace delosfera_server.Modules.Help.Models;

/// <summary>
/// Изображение, использованное в статье инструкции.
///
/// Связь ведётся отдельной записью, а не выводится из тела статьи. Тело —
/// произвольный JSON, и проверять по нему право на файл значило бы разбирать
/// разметку при каждой выдаче картинки. Здесь же на вопрос «можно ли этому
/// сотруднику этот файл» отвечает одна строка таблицы.
///
/// Без такой связи скриншот инструкции видел бы только тот, кто его загрузил:
/// общее правило доступа к файлам разрешает их автору вложения и участникам
/// документа, а инструкция не документ и участников не имеет.
/// </summary>
public class HelpArticleImage
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public HelpArticle? Article { get; set; }

    public int FileId { get; set; }
    public Files.Models.FileAttachment? File { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
