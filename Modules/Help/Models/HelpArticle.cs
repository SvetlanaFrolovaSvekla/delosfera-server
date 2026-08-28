namespace delosfera_server.Modules.Help.Models;

/// <summary>
/// Раздел справки — соответствует контуру системы, а не структуре меню.
///
/// Человек ищет инструкцию по тому, что он делает («мне надо согласовать записку»),
/// а не по тому, где лежит экран. Поэтому разделы названы работой, а не пунктами меню.
/// </summary>
public enum HelpSection
{
    /// <summary>С чего начать: вход, рабочий стол, задачи.</summary>
    Start = 0,

    /// <summary>Служебные записки.</summary>
    Sz = 1,

    /// <summary>Внутренние нормативные документы.</summary>
    Vnd = 2,

    /// <summary>Закупки: заявка, процедура, договор.</summary>
    Procurement = 3,

    /// <summary>Согласование и подписание.</summary>
    Signing = 4,

    /// <summary>Заседания и поручения.</summary>
    Meetings = 5,

    /// <summary>Администрирование: пользователи, права, справочники.</summary>
    Administration = 6,
}

/// <summary>
/// Статья инструкции.
///
/// Тело хранится не текстом, а списком блоков: шаг, предупреждение, ссылка в нужный
/// раздел системы, ссылка на действующий ВНД. Из-за этого статья умеет то, чего не
/// умеет текст — довести человека до экрана, о котором рассказывает, и остаться
/// связанной с Положением, на которое ссылается.
///
/// Тексты правит администратор прямо в системе: инструкция устаревает быстрее, чем
/// выходит очередная сборка, и держать её в коде значит гарантировать расхождение
/// с тем, что человек видит на экране.
/// </summary>
public class HelpArticle
{
    public int Id { get; set; }

    public HelpSection Section { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Одна строка о том, на какой вопрос отвечает статья — видна в оглавлении.</summary>
    public string? SummaryRu { get; set; }
    public string? SummaryKg { get; set; }

    /// <summary>Тело статьи: список блоков в JSON. Схема блоков — в HelpBlock.</summary>
    public string BodyJson { get; set; } = "[]";

    /// <summary>
    /// Маршрут экрана, к которому статья относится: «/prc», «/sz». По нему у нужной
    /// страницы появляется кнопка справки, ведущая сразу сюда, а не в общее оглавление.
    /// </summary>
    public string? RoutePath { get; set; }

    /// <summary>Порядок в разделе. Одинаковые значения — сортировка по названию.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Черновик не показывается сотрудникам. Инструкцию пишут не за один заход, и
    /// половина статьи хуже, чем её отсутствие: человек уйдёт, решив, что тут пусто.
    /// </summary>
    public bool IsPublished { get; set; }

    public int? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Снимки экрана, использованные в статье.</summary>
    public ICollection<HelpArticleImage> Images { get; set; } = new List<HelpArticleImage>();
}

/// <summary>
/// Вид блока в теле статьи. Набор намеренно узкий: чем больше видов, тем больше
/// решений у того, кто пишет инструкцию, и тем менее одинаково выглядят статьи.
/// </summary>
public static class HelpBlockKind
{
    /// <summary>Абзац текста.</summary>
    public const string Text = "text";

    /// <summary>Нумерованные шаги — то, что человек делает по порядку.</summary>
    public const string Steps = "steps";

    /// <summary>Замечание: обычное пояснение или предупреждение.</summary>
    public const string Note = "note";

    /// <summary>Кнопка, ведущая в раздел системы.</summary>
    public const string Link = "link";

    /// <summary>Ссылка на действующий документ в базе ВНД.</summary>
    public const string Vnd = "vnd";

    /// <summary>
    /// Снимок экрана с нумерованными выносками: «нажмите 1, затем 2».
    ///
    /// Выноски нумерованные, а не нарисованные стрелки: стрелку пришлось бы
    /// рисовать в графическом редакторе и перерисовывать при каждой правке
    /// интерфейса, а номер ставится мышью прямо в системе и переносится вместе
    /// с изображением.
    /// </summary>
    public const string Image = "image";
}
