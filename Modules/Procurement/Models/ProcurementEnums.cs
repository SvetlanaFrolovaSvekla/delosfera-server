namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Способ закупки (PRC-02). Коды фиксированы, потому что на них завязано ветвление
/// процедуры; наименования и параметры порогов настраиваются администратором.
/// </summary>
public enum ProcurementMethodCode
{
    /// <summary>Прямое заключение договора — только по основаниям п. 6.6 Положения.</summary>
    Direct = 1,

    /// <summary>Простая закупка: запрос ценовых предложений / анализ рынка, не менее 3 КП.</summary>
    Simple = 2,

    /// <summary>Конкурс с неограниченным участием — с публикацией объявления.</summary>
    TenderOpen = 3,

    /// <summary>Конкурс с ограниченным участием — приглашения определённому кругу поставщиков.</summary>
    TenderLimited = 4,
}

/// <summary>
/// От чего считается порог правила матрицы. Положение задаёт пороги тремя способами,
/// и все три должны пересчитываться при изменении баланса, а не переписываться руками.
/// </summary>
public enum ThresholdBase
{
    /// <summary>Абсолютная сумма в сомах.</summary>
    Absolute = 1,

    /// <summary>Процент балансовой стоимости активов (конкурс: 20%, 50%).</summary>
    PercentOfAssets = 2,

    /// <summary>Процент чистого собственного капитала — пороги по аффилированным лицам.</summary>
    PercentOfNsk = 3,
}

/// <summary>Орган, утверждающий расход по итогам закупки.</summary>
public enum ApprovalAuthority
{
    /// <summary>Утверждение не требуется — расход в пределах решения инициатора.</summary>
    None = 0,

    /// <summary>Куратор — курирующий член Правления.</summary>
    Curator = 1,

    /// <summary>Правление Банка.</summary>
    Board = 2,

    /// <summary>Совет директоров.</summary>
    SupervisoryBoard = 3,

    /// <summary>Общее собрание акционеров.</summary>
    Shareholders = 4,
}
