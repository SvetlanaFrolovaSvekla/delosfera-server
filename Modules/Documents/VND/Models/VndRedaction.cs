using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Users.Models;


namespace delosfera_server.Modules.Documents.VND.Models;

public class VndRedaction : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public string? Description { get; set; } // Описание редакции

    /// <summary>Порядковый номер редакции в рамках ВНД, авто-инкремент (1, 2, 3...)</summary>
    public int Number { get; set; }

    /// <summary>Код редакции, авто: {КодВНД}-Р{Number}, например "10062-Р3"</summary>
    public required string Code { get; set; }

    // --- Основные файлы редакции (DOC/DOCX)
    public int DocFileRuId { get; set; }
    public FileAttachment? DocFileRu { get; set; }

    public int? DocFileKgId { get; set; }
    public FileAttachment? DocFileKg { get; set; }

    public int? DocFileEnId { get; set; }
    public FileAttachment? DocFileEn { get; set; }

    /// <summary>Когда документ на соответствующем языке в последний раз заменялся файлом
    /// (в т.ч. при повторной отправке после замечаний — см. ResubmitAfterRevisionAsync).
    /// Null, если документ ни разу не заменялся после создания редакции — то есть это
    /// исходный файл, приложенный при создании редакции (см. CreatedAt в этом случае).
    /// Используется на фронте для метки "Обновлено, дата" рядом с документом редакции.</summary>
    public DateTime? DocRuUpdatedAt { get; set; }
    public DateTime? DocKgUpdatedAt { get; set; }
    public DateTime? DocEnUpdatedAt { get; set; }

    /// <summary>Таблица изменений и дополнений (ТИД) — Word-файл, обязателен, если у ВНД уже была
    /// предыдущая редакция (Number > 1, то есть документ актуализируется, а не создаётся впервые).
    /// При повторной отправке после замечаний (ResubmitAfterRevisionAsync) обновляется тем же файлом
    /// или новым, если инициатор его заменил.</summary>
    public int? TidFileId { get; set; }
    public FileAttachment? TidFile { get; set; }

    // --- Согласование
    public bool RequiresApproval { get; set; }
    public RedactionApprovalStatus ApprovalStatus { get; set; } = RedactionApprovalStatus.NotRequired;

    // --- Прочие вложения (Word/Excel/презентации и др.)
    public ICollection<VndRedactionAttachment> Attachments { get; set; } = new List<VndRedactionAttachment>();

    /// <summary>Лист согласования — формируется автоматически по шаблону (см.
    /// ApprovalSheetGenerator) в момент, когда согласование редакции окончательно завершается
    /// (см. VndApprovalService.FinalizeApprovalAsync). Null, пока редакция не согласована. Это
    /// отдельное "специальное" вложение — не входит в Attachments, показывается в интерфейсе в
    /// отдельном блоке "Специальные вложения".</summary>
    public int? ApprovalSheetFileId { get; set; }
    public FileAttachment? ApprovalSheetFile { get; set; }

    /// <summary>Заголовок и вид документа НА МОМЕНТ ЭТОЙ редакции (этап "заголовок/вид тоже по
    /// редакции", см. обсуждение) — переименование/смена вида документа не переписывают задним
    /// числом то, как назывались/классифицировались прошлые редакции.</summary>
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public int TypeId { get; set; }
    public TypeVnd? Type { get; set; }

    // --- Реквизиты редакции (этап 1 миграции "реквизиты по редакции", см. обсуждение) ---
    // У каждой редакции теперь СВОИ реквизиты: дата/номер утверждения, вступление в силу,
    // классификаторы и т.д. — а не общие на весь ВНД. Это даёт полную историю: можно
    // посмотреть, какие реквизиты были именно у Р1, у Р2 и т.д., где появился/исчез рубрикатор
    // или ключевое слово между редакциями.
    //
    // ВАЖНО (переходный период): одноимённые поля пока ЕЩЁ остаются и на VndDocument —
    // они не убраны специально, чтобы ничего не сломать в существующих местах чтения/поиска
    // до отдельного финального шага миграции. Актуальным источником правды нужно считать
    // ИМЕННО поля здесь, на VndRedaction (а точнее — на VndDocument.CurrentRedaction).
    // Поля на VndDocument будут удалены отдельным шагом, когда все места чтения переключатся
    // на редакцию.

    /// <summary>Дата и номер утверждения ЭТОЙ редакции органом утверждения (см. AdoptionCode
    /// формата "46(6)" — номер протокола и номер вопроса повестки). Заполняется при
    /// консолидации редакции (см. VndActualizationService.PublishAsync).</summary>
    public DateOnly? AdoptionDate { get; set; }
    public string? AdoptionCode { get; set; }

    /// <summary>Дата, с которой ИМЕННО ЭТА редакция становится действующей. До этого момента,
    /// даже если ВНД уже в статусе Active, документ отображается как "Ожидание вступления
    /// в силу" (см. isVndPendingEffective на клиенте).</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Периодичность плановой актуализации, действовавшая для ЭТОЙ редакции.</summary>
    public ActualizationPeriod Period { get; set; }

    public int DeveloperId { get; set; } // СП-разработчик на момент этой редакции
    public OrganizationUnit? Developer { get; set; }

    public int? CuratorDeveloperId { get; set; } // Куратор разработчика на момент этой редакции
    public User? CuratorDeveloper { get; set; }

    public int OrganId { get; set; } // Орган утверждения этой редакции
    public ApprovalBody? Organ { get; set; }

    public int SecrecyLevelId { get; set; }
    public SecurityLevel? SecrecyLevel { get; set; }

    // Ответственные исполнители - начальник выбранного СП, действовавшие на момент этой редакции
    public ICollection<OrganizationUnit> ResponsibleExecutors { get; set; } = new List<OrganizationUnit>();

    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
    public ICollection<Keyword> Keywords { get; set; } = new List<Keyword>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}