namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Тип документа в едином реестре (GEN-05). Каждый контур — свой тип.
/// </summary>
public enum DocumentType
{
    /// <summary>Служебная записка</summary>
    Sz = 1,

    /// <summary>Внутренний нормативный документ</summary>
    Vnd = 2,

    /// <summary>Таблица изменений и дополнений к ВНД</summary>
    Tid = 3,

    /// <summary>Заявка/процесс закупки</summary>
    Procurement = 4,

    /// <summary>Договор</summary>
    Contract = 5
}
