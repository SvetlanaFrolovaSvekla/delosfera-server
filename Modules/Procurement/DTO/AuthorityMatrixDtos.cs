using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Запрос на подбор способа закупки по Матрице полномочий (PRC-04).</summary>
public class MatrixResolveRequest
{
    /// <summary>Сумма закупки в сомах.</summary>
    public decimal Amount { get; set; }

    /// <summary>Сделка с аффилированным лицом — переключает шкалу на проценты ЧСК.</summary>
    public bool IsAffiliated { get; set; }

    /// <summary>
    /// Способ, выбранный инициатором. Пусто — система подбирает сама.
    /// Прямое заключение выбирается вручную и только с обоснованием.
    /// </summary>
    public ProcurementMethodCode? PreferredMethod { get; set; }
}

/// <summary>Строка «факта» под карточкой результата: порог, от которого сработало правило.</summary>
public record MatrixFactDto(string Key, string Value, bool IsHighlighted);

/// <summary>Результат подбора: способ, состав согласования, комиссия, орган утверждения.</summary>
public class MatrixResolveResponse
{
    public required string MethodCode { get; set; }
    public required string MethodTitle { get; set; }
    public required string MethodShortTitle { get; set; }

    /// <summary>Альтернативный допустимый способ при той же сумме — например прямое заключение по основанию.</summary>
    public string? AlternativeMethodTitle { get; set; }

    public required string ApprovalChain { get; set; }
    public bool CommissionRequired { get; set; }
    public int? CommissionSize { get; set; }
    public int? CommissionMinBoardMembers { get; set; }
    public required string CommissionNote { get; set; }

    public ApprovalAuthority ApprovalAuthority { get; set; }
    public required string ApprovalAuthorityTitle { get; set; }

    /// <summary>Нужен ли протокол закупки (PRC-10) при этой сумме.</summary>
    public bool ProtocolRequired { get; set; }
    public decimal ProtocolThreshold { get; set; }

    public int MinProposals { get; set; }
    public bool RequiresJustification { get; set; }
    public bool RequiresPublication { get; set; }

    /// <summary>Пороги и база расчёта — показываются под результатом, чтобы решение было проверяемым.</summary>
    public List<MatrixFactDto> Facts { get; set; } = [];

    /// <summary>Дополнительные требования: УБУиО в комиссии, председатель — член Правления.</summary>
    public List<MatrixNoteResponse> Notes { get; set; } = [];

    /// <summary>
    /// Положение о закупках в базе ВНД. Нужен экранам, которые сами показывают
    /// правила Положения — например, подсказку про закупку вне бюджета.
    /// </summary>
    public int? RegulationDocumentId { get; set; }

    /// <summary>Id сработавшего правила — на него ссылается заявка, чтобы решение было воспроизводимо.</summary>
    public int? RuleId { get; set; }
}

/// <summary>Строка приложения №1 для табличного вывода матрицы.</summary>
public class MatrixRuleDto
{
    public int Id { get; set; }
    public required string MethodTitle { get; set; }
    public required string MethodShortTitle { get; set; }
    public bool IsAffiliated { get; set; }

    /// <summary>Диапазон человекочитаемо: «от 500 000 сом до 20% активов».</summary>
    public required string RangeTitle { get; set; }

    /// <summary>Границы, пересчитанные в сомы на текущие баланс и ЧСК.</summary>
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public required string ApprovalChain { get; set; }
    public required string CommissionNote { get; set; }
    public required string ApprovalAuthorityTitle { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Матрица целиком плюс параметры, от которых считаются пороги.</summary>
public class MatrixTableDto
{
    public List<MatrixRuleDto> Regular { get; set; } = [];
    public List<MatrixRuleDto> Affiliated { get; set; } = [];

    public decimal BalanceAssets { get; set; }
    public decimal Nsk { get; set; }
    public decimal ProtocolThreshold { get; set; }
}

/// <summary>
/// Примечание к решению матрицы. Правило взято не из воздуха, а из Положения о
/// закупках, поэтому рядом с текстом идёт пункт и ссылка на сам документ: человек
/// должен иметь возможность прочитать основание, а не верить системе на слово.
/// </summary>
public class MatrixNoteResponse
{
    public required string Text { get; set; }

    /// <summary>Пункт Положения, на котором держится правило.</summary>
    public string? Clause { get; set; }

    /// <summary>Документ Положения в базе ВНД, если он туда загружен.</summary>
    public int? DocumentId { get; set; }
}

/// <summary>
/// Правило матрицы в сыром виде — для экрана настройки, а не для показа. Здесь
/// пороги и состав лежат полями, которые редактируют, а не строкой «от…до».
/// </summary>
public class MatrixRuleEditDto
{
    public int Id { get; set; }

    public int MethodId { get; set; }
    public required string MethodShortTitle { get; set; }
    public bool IsAffiliated { get; set; }

    public decimal? MinValue { get; set; }
    public ThresholdBase MinBase { get; set; }
    public decimal? MaxValue { get; set; }
    public ThresholdBase MaxBase { get; set; }

    public required string ApprovalChainRu { get; set; }
    public ApprovalAuthority ApprovalAuthority { get; set; }
    public bool CommissionRequired { get; set; }
    public int? CommissionSize { get; set; }
    public int? CommissionMinBoardMembers { get; set; }
    public required string CommissionNoteRu { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Что можно изменить в правиле матрицы.</summary>
public class MatrixRuleSaveRequest
{
    public int MethodId { get; set; }
    public bool IsAffiliated { get; set; }

    public decimal? MinValue { get; set; }
    public ThresholdBase MinBase { get; set; } = ThresholdBase.Absolute;
    public decimal? MaxValue { get; set; }
    public ThresholdBase MaxBase { get; set; } = ThresholdBase.Absolute;

    public string ApprovalChainRu { get; set; } = "";
    public ApprovalAuthority ApprovalAuthority { get; set; }
    public bool CommissionRequired { get; set; }
    public int? CommissionSize { get; set; }
    public int? CommissionMinBoardMembers { get; set; }
    public string CommissionNoteRu { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Способ закупки для настройки: минимум КП и подписи.</summary>
public class ProcurementMethodEditDto
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string TitleRu { get; set; }
    public required string ShortTitleRu { get; set; }

    /// <summary>Сколько коммерческих предложений минимум. 0 — не требуется.</summary>
    public int MinProposals { get; set; }
    public bool RequiresJustification { get; set; }
    public bool RequiresPublication { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Что можно изменить в способе закупки. Код и признаки процедуры неизменны.</summary>
public class ProcurementMethodSaveRequest
{
    public string TitleRu { get; set; } = "";
    public string ShortTitleRu { get; set; } = "";
    public int MinProposals { get; set; }
    public bool IsActive { get; set; } = true;
}
