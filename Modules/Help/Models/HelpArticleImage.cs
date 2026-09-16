namespace delosfera_server.Modules.Help.Models;

/// <summary>
/// Файл, использованный в статье инструкции — снимок экрана (блок "image") или
/// приложенный документ вроде .docx/.pdf (блок "file"). Несмотря на имя класса,
/// таблица общая для обоих: ей нужен только сам факт "этот FileId привязан к
/// этой статье", а какого рода файл — решает контроллер (HelpController.GetImage
/// против GetFile), не эта запись.
///
/// Связь ведётся отдельной записью, а не выводится из тела статьи. Тело —
/// произвольный JSON, и проверять по нему право на файл значило бы разбирать
/// разметку при каждой выдаче. Здесь же на вопрос «можно ли этому сотруднику
/// этот файл» отвечает одна строка таблицы.
///
/// Без такой связи вложение инструкции видел бы только тот, кто его загрузил:
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
