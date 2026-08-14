using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

/// <summary>
/// Движок Матрицы полномочий (PRC-04/05). По сумме, признаку аффилированности и —
/// если инициатор его задал — выбранному способу находит правило Положения и отдаёт
/// состав согласования, требования к комиссии и орган утверждения расхода.
///
/// Пороги в правилах хранятся в трёх шкалах (сомы, % активов, % ЧСК), поэтому сравнение
/// всегда идёт после приведения границ к сомам по текущим параметрам.
/// </summary>
public class AuthorityMatrixService : IAuthorityMatrixService
{
    private readonly DelosferaDbContext _db;

    public AuthorityMatrixService(DelosferaDbContext db) => _db = db;

    public async Task<MatrixResolveResponse> ResolveAsync(MatrixResolveRequest request)
    {
        if (request.Amount < 0)
            throw new ArgumentException("Сумма закупки не может быть отрицательной");

        var p = await LoadParametersAsync();
        var rules = await _db.AuthorityMatrixRules
            .Include(x => x.Method)
            .Where(x => x.IsActive && x.IsAffiliated == request.IsAffiliated)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var matching = rules
            .Where(r => InRange(r, request.Amount, p))
            .ToList();

        if (matching.Count == 0)
            throw new InvalidOperationException(
                "Для указанной суммы в Матрице полномочий нет действующего правила — проверьте пороги в справочнике");

        // Инициатор может настоять на способе (прямое заключение по п. 6.6) — если для этой
        // суммы правило есть, берём его. Иначе способ определяет сама матрица.
        var rule = request.PreferredMethod is { } preferred
            ? matching.FirstOrDefault(r => r.Method!.Code == preferred) ?? PickDefault(matching)
            : PickDefault(matching);

        var alternative = matching.FirstOrDefault(r => r.Id != rule.Id);
        var method = rule.Method!;

        var response = new MatrixResolveResponse
        {
            RuleId = rule.Id,
            MethodCode = method.Code.ToString(),
            MethodTitle = method.TitleRu,
            MethodShortTitle = method.ShortTitleRu,
            AlternativeMethodTitle = alternative?.Method?.TitleRu,
            ApprovalChain = rule.ApprovalChainRu,
            CommissionRequired = rule.CommissionRequired,
            CommissionSize = rule.CommissionSize,
            CommissionMinBoardMembers = rule.CommissionMinBoardMembers,
            CommissionNote = rule.CommissionNoteRu,
            ApprovalAuthority = rule.ApprovalAuthority,
            ApprovalAuthorityTitle = AuthorityTitle(rule.ApprovalAuthority),
            ProtocolThreshold = p.ProtocolThreshold,
            ProtocolRequired = request.Amount > p.ProtocolThreshold,
            MinProposals = method.MinProposals,
            RequiresJustification = method.RequiresJustification,
            RequiresPublication = method.RequiresPublication,
        };

        response.Facts.Add(new MatrixFactDto("Сумма закупки", Money(request.Amount), true));
        response.Facts.Add(new MatrixFactDto("Диапазон правила", RangeTitle(rule, p), false));
        response.Facts.Add(new MatrixFactDto("Согласование закупки", rule.ApprovalChainRu, false));
        response.Facts.Add(new MatrixFactDto("Утверждение расхода", AuthorityTitle(rule.ApprovalAuthority), false));

        if (request.IsAffiliated)
            response.Facts.Add(new MatrixFactDto("Доля от ЧСК", Percent(request.Amount, p.Nsk), false));
        else if (rule.MinBase == ThresholdBase.PercentOfAssets || rule.MaxBase == ThresholdBase.PercentOfAssets)
            response.Facts.Add(new MatrixFactDto("Доля от активов", Percent(request.Amount, p.BalanceAssets), false));

        FillNotes(response, rule, method, request.Amount, p);
        return response;
    }

    public async Task<MatrixTableDto> GetTableAsync()
    {
        var p = await LoadParametersAsync();
        var rules = await _db.AuthorityMatrixRules
            .Include(x => x.Method)
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        MatrixRuleDto Map(AuthorityMatrixRule r) => new()
        {
            Id = r.Id,
            MethodTitle = r.Method!.TitleRu,
            MethodShortTitle = r.Method.ShortTitleRu,
            IsAffiliated = r.IsAffiliated,
            RangeTitle = RangeTitle(r, p),
            MinAmount = ToAmount(r.MinValue, r.MinBase, p),
            MaxAmount = ToAmount(r.MaxValue, r.MaxBase, p),
            ApprovalChain = r.ApprovalChainRu,
            CommissionNote = r.CommissionNoteRu,
            ApprovalAuthorityTitle = AuthorityTitle(r.ApprovalAuthority),
            SortOrder = r.SortOrder,
        };

        return new MatrixTableDto
        {
            Regular = rules.Where(r => !r.IsAffiliated).Select(Map).ToList(),
            Affiliated = rules.Where(r => r.IsAffiliated).Select(Map).ToList(),
            BalanceAssets = p.BalanceAssets,
            Nsk = p.Nsk,
            ProtocolThreshold = p.ProtocolThreshold,
        };
    }

    /// <summary>
    /// Из нескольких подходящих правил берём то, где закупка конкурентная: Положение
    /// допускает прямое заключение лишь по отдельному основанию, поэтому по умолчанию
    /// система не должна предлагать его сама.
    /// </summary>
    private static AuthorityMatrixRule PickDefault(List<AuthorityMatrixRule> matching) =>
        matching.FirstOrDefault(r => r.Method!.Code != ProcurementMethodCode.Direct) ?? matching[0];

    /// <summary>
    /// Диапазон полуоткрытый: нижняя граница включается, верхняя — нет.
    ///
    /// В Положении соседние пороги записаны как «до 500 000» и «от 500 000», и при
    /// включении обеих границ ровно на 500 000 подходили сразу два правила. Побеждало
    /// то, что стоит раньше по порядку, то есть более слабая процедура: пороговая
    /// сумма уходила на упрощённую закупку вместо конкурса с комиссией.
    ///
    /// Диапазоны правил стыкуются встык (1–500 000, 500 000–20% активов, 20–50%,
    /// от 50%), поэтому исключение верхней границы не оставляет сумм без правила.
    /// </summary>
    private static bool InRange(AuthorityMatrixRule rule, decimal amount, MatrixParameters p)
    {
        var min = ToAmount(rule.MinValue, rule.MinBase, p);
        var max = ToAmount(rule.MaxValue, rule.MaxBase, p);

        if (min is { } lo && amount < lo) return false;
        if (max is { } hi && amount >= hi) return false;
        return true;
    }

    private static decimal? ToAmount(decimal? value, ThresholdBase basis, MatrixParameters p) => value switch
    {
        null => null,
        _ => basis switch
        {
            ThresholdBase.Absolute => value,
            ThresholdBase.PercentOfAssets => p.BalanceAssets * value.Value / 100m,
            ThresholdBase.PercentOfNsk => p.Nsk * value.Value / 100m,
            _ => value,
        },
    };

    private static void FillNotes(
        MatrixResolveResponse response, AuthorityMatrixRule rule, ProcurementMethod method,
        decimal amount, MatrixParameters p)
    {
        // Каждое правило опирается на пункт Положения о закупках. Пункт и ссылка на
        // документ идут рядом с текстом: инициатор должен прочитать основание сам,
        // а не верить системе на слово.
        response.RegulationDocumentId = p.RegulationDocumentId;

        void Note(string text, string? clause = null) =>
            response.Notes.Add(new MatrixNoteResponse
            {
                Text = text,
                Clause = clause,
                DocumentId = clause is null ? null : p.RegulationDocumentId,
            });

        if (method.MinProposals > 0)
            Note($"Требуется не менее {method.MinProposals} коммерческих предложений (PRC-09)",
                "п. 8.1 Положения — запрос ценовых предложений");

        if (method.RequiresJustification)
            Note("Обязательно обоснование применения способа по п. 6.6 Положения", "п. 6.6 Положения");

        if (method.RequiresPublication)
            Note("Объявление публикуется на сайте Банка и tenders.kg, конкурсный период — не менее 5 рабочих дней",
                "разделы 11.2–11.3 Положения — конкурс");

        if (response.ProtocolRequired)
            Note($"Сумма превышает {Money(p.ProtocolThreshold)} — оформляется протокол закупки (PRC-10)",
                "п. 11.1 Положения и Приложение № 2");

        if (rule.CommissionRequired && amount > p.CommissionBoardChairThreshold)
            Note($"Свыше {Money(p.CommissionBoardChairThreshold)} председателем комиссии назначается член Правления, не курирующий инициирующее СП");

        if (rule.CommissionRequired && amount > p.CommissionAccountantThreshold)
            Note($"Свыше {Money(p.CommissionAccountantThreshold)} в состав комиссии включается сотрудник УБУиО");

        if (rule.ApprovalAuthority is ApprovalAuthority.Board or ApprovalAuthority.SupervisoryBoard or ApprovalAuthority.Shareholders)
            Note($"Этап «Вынесение на {AuthorityTitle(rule.ApprovalAuthority)}»: продолжение — после загрузки выписки из протокола (PRC-06)");
    }

    private static string RangeTitle(AuthorityMatrixRule rule, MatrixParameters p)
    {
        string Bound(decimal? v, ThresholdBase basis) => basis switch
        {
            ThresholdBase.PercentOfAssets => $"{Trim(v!.Value)}% активов",
            ThresholdBase.PercentOfNsk => $"{Trim(v!.Value)}% ЧСК",
            _ => Money(v!.Value),
        };

        return (rule.MinValue, rule.MaxValue) switch
        {
            (null, null) => "любая сумма",
            (not null, null) => $"от {Bound(rule.MinValue, rule.MinBase)}",
            (null, not null) => $"до {Bound(rule.MaxValue, rule.MaxBase)}",
            _ => $"от {Bound(rule.MinValue, rule.MinBase)} до {Bound(rule.MaxValue, rule.MaxBase)}",
        };
    }

    private static string AuthorityTitle(ApprovalAuthority authority) => authority switch
    {
        ApprovalAuthority.Curator => "Куратор",
        ApprovalAuthority.Board => "Правление",
        ApprovalAuthority.SupervisoryBoard => "Совет директоров",
        ApprovalAuthority.Shareholders => "Общее собрание акционеров",
        _ => "Не требуется",
    };

    private static string Money(decimal value) => $"{value:### ### ### ##0.##} сом".Trim();

    private static string Percent(decimal amount, decimal basis) =>
        basis <= 0 ? "—" : $"{Trim(amount / basis * 100m)}%";

    private static string Trim(decimal value) =>
        value == decimal.Truncate(value) ? decimal.Truncate(value).ToString() : value.ToString("0.####");

    private async Task<MatrixParameters> LoadParametersAsync()
    {
        var raw = await _db.ProcurementParameters.ToDictionaryAsync(x => x.Code, x => x.Value);

        decimal Get(string code, decimal fallback) => raw.TryGetValue(code, out var v) ? v : fallback;

        return new MatrixParameters(
            BalanceAssets: Get("BalanceAssets", 0m),
            Nsk: Get("Nsk", 0m),
            ProtocolThreshold: Get("ProtocolThreshold", 50_000m),
            CommissionAccountantThreshold: Get("CommissionAccountantThreshold", 5_000_000m),
            CommissionBoardChairThreshold: Get("CommissionBoardChairThreshold", 3_000_000m),
            // Ноль означает «Положение в базу ВНД ещё не загружено»: тогда примечание
            // остаётся текстом с номером пункта, но без ссылки.
            RegulationDocumentId: Get("RegulationDocumentId", 0m) is var id && id > 0 ? (int)id : null);
    }

    private record MatrixParameters(
        decimal BalanceAssets,
        decimal Nsk,
        decimal ProtocolThreshold,
        decimal CommissionAccountantThreshold,
        decimal CommissionBoardChairThreshold,
        int? RegulationDocumentId);
}
