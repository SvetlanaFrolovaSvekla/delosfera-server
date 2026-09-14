namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Личный шаблон служебной записки (СЗ-6): сохранённый пресет полей формы, который
/// автор применяет при создании новой записки, чтобы не заполнять одно и то же руками.
///
/// Шаблон личный — принадлежит автору: у каждого свой набор регулярных записок
/// (заявки на доступ, на закупку, кадровые), и общий список замусорил бы форму
/// чужими заготовками. Общие шаблоны подразделения — отдельный разговор.
///
/// Поля пресета лежат одной строкой JSON, а не отдельными колонками и таблицами
/// связей: набор полей записки широк и зависит от вида, а шаблон лишь подставляет
/// значения в форму — раскладывать их по схеме БД смысла нет.
/// </summary>
public class SzTemplate
{
    public int Id { get; set; }

    /// <summary>Автор шаблона — видит и правит только он.</summary>
    public int OwnerUserId { get; set; }

    public required string Name { get; set; }

    /// <summary>Пресет полей записки, сериализованный JSON (SzTemplatePayload).</summary>
    public required string Payload { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
