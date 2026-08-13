using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

using NpgsqlTypes;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Единая карточка документа в реестре (GEN-05: документ хранится однократно,
/// все связи ссылаются на этот объект). Контурные сущности (СЗ, ВНД, закупка)
/// ссылаются на Document 1:1 и добавляют собственные поля.
/// </summary>
public class Document : IAuditableEntity
{
    public int Id { get; set; }

    public DocumentType Type { get; set; }

    /// <summary>Регистрационный номер (нумератор, GEN-09). Null до регистрации.</summary>
    public string? RegNumber { get; set; }

    public required string Title { get; set; }

    /// <summary>Настраиваемый тип документа, если Type = Custom (GEN-06).</summary>
    public int? DefinitionId { get; set; }
    public DocumentTypeDefinition? Definition { get; set; }

    /// <summary>
    /// Значения полей настраиваемой карточки — json по кодам полей. Хранятся одним
    /// документом, а не таблицей «поле-значение»: набор полей задаёт администратор,
    /// и колонка на каждое поле означала бы миграцию на каждую правку настроек.
    /// </summary>
    public string? FieldValues { get; set; }

    /// <summary>
    /// Поисковый вектор по наименованию и регистрационному номеру (GEN-04).
    /// Вычисляется самой базой: отдельная синхронизация индекса рано или поздно
    /// расходится с данными, а генерируемая колонка не может устареть.
    /// </summary>
    public NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>Текущий статус (строковый код, набор зависит от типа документа).</summary>
    public required string StatusCode { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    /// <summary>Активный экземпляр маршрута согласования (Workflow), если запущен.</summary>
    public int? CurrentRouteInstanceId { get; set; }

    /// <summary>Признак «Бумажный носитель (по Перечню НБКР)» (SZ-PAP-01).</summary>
    public bool IsPaperCarrier { get; set; }

    // --- Архивное хранение (GEN-09) ---

    /// <summary>Дело номенклатуры, в которое подшит документ.</summary>
    public int? NomenclatureCaseId { get; set; }
    public NomenclatureCase? NomenclatureCase { get; set; }

    /// <summary>
    /// Срок хранения. Берётся из дела при подшивке, но может быть переопределён:
    /// отдельные документы хранятся дольше, чем дело в целом.
    /// </summary>
    public int? StorageTermId { get; set; }
    public StorageTerm? StorageTerm { get; set; }

    public DateOnly? ArchivedOn { get; set; }

    /// <summary>
    /// Год, после которого документ можно уничтожить. Пусто при постоянном хранении
    /// либо пока дело не закрыто — срок считается от закрытия дела.
    /// </summary>
    public int? DestroyAfterYear { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Токен версии для оптимистичной блокировки (GEN-05). Обновляется при каждом
    /// сохранении карточки.
    ///
    /// Системная колонка xmin для этого не годится: провайдер пытается завести её
    /// заново миграцией, хотя она есть у каждой строки Postgres. Обычное поле
    /// переносимо и не зависит от особенностей базы.
    /// </summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();
}
