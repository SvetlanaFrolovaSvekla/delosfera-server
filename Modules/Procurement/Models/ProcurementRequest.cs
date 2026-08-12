using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

using NpgsqlTypes;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Заявка на закупку (PRC-01). Номер, статус, автор, вложения и аудит живут в единой
/// карточке документа (GEN-05), здесь — закупочные поля: предмет, сумма, способ,
/// бюджет и решение Матрицы полномочий, по которому построен маршрут.
///
/// Заявка появляется двумя путями: из служебной записки вида «на закупку»
/// (SzProcurementService заводит документ-заготовку) либо напрямую из реестра закупок.
/// </summary>
public class ProcurementRequest : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Единая карточка документа: номер, статус, автор, вложения, аудит.</summary>
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Предмет закупки: «Ноутбуки бизнес-класса (8 шт.)».</summary>
    public required string Subject { get; set; }

    /// <summary>Обоснование необходимости: цель, риски при непринятии решения, эффективность.</summary>
    public string? Justification { get; set; }

    /// <summary>Поисковый вектор по предмету, обоснованию и позиции плана (GEN-04).</summary>
    public NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>Тип предмета — от него зависит ветвление маршрута (PRC-08, вопрос В-5).</summary>
    public ProcurementSubjectKind SubjectKind { get; set; }

    /// <summary>Ориентировочная сумма закупки в сомах.</summary>
    public decimal Amount { get; set; }

    /// <summary>Сделка с аффилированным лицом — переводит пороги на шкалу ЧСК.</summary>
    public bool IsAffiliated { get; set; }

    /// <summary>Предусмотрено бюджетом Банка; иначе идёт отдельная ветка согласования.</summary>
    public bool HasBudget { get; set; }

    /// <summary>Позиция утверждённого Плана закупок (PRC-03); пусто — внеплановая закупка.</summary>
    public string? PlanItem { get; set; }

    /// <summary>Техническое задание приложено файлом или структурированным текстом.</summary>
    public bool HasSpecification { get; set; }

    /// <summary>Инициирующее структурное подразделение.</summary>
    public int? InitiatorUnitId { get; set; }
    public OrganizationUnit? InitiatorUnit { get; set; }

    /// <summary>Куратор — курирующий член Правления.</summary>
    public int? CuratorUserId { get; set; }
    public User? CuratorUser { get; set; }

    // --- решение Матрицы полномочий на момент создания (PRC-04) ---

    /// <summary>Способ закупки: подобран матрицей либо выбран инициатором с обоснованием.</summary>
    public int MethodId { get; set; }
    public ProcurementMethod? Method { get; set; }

    /// <summary>
    /// Правило матрицы, по которому определены согласование и орган утверждения.
    /// Хранится, чтобы решение оставалось воспроизводимым после правки порогов.
    /// </summary>
    public int? MatrixRuleId { get; set; }
    public AuthorityMatrixRule? MatrixRule { get; set; }

    /// <summary>Состав согласования на момент создания заявки.</summary>
    public string? ApprovalChain { get; set; }

    /// <summary>Орган, утверждающий расход.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; }

    /// <summary>Обоснование выбора способа — обязательно для прямого заключения (п. 6.6).</summary>
    public string? MethodJustification { get; set; }

    /// <summary>Требуется ли протокол закупки при этой сумме (PRC-10).</summary>
    public bool ProtocolRequired { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
