using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

/// <summary>Нужен ли по этой закупке договор и почему.</summary>
public sealed record ContractDecision(bool Required, string Reason);

/// <summary>
/// Условия применения закупок без заключения договора (раздел VII Положения).
///
/// Мелкая закупка договора не требует: достаточно согласованной заявки. Но
/// «мелкая» здесь не сводится к сумме — Положение называет три случая, когда
/// договор нужен независимо от неё, и порядок проверки важен. Сумма проверяется
/// последней: иначе закупка на тысячу сом у поставщика-нерезидента прошла бы без
/// договора, хотя п. 10.4 требует обратного.
///
/// Логика вынесена отдельно и без обращений к базе: решение читается целиком в
/// одном месте и проверяется без поднятого приложения.
/// </summary>
public static class ContractRequirement
{
    /// <summary>Товары — до пятидесяти тысяч сом без договора (п. 10.1.1).</summary>
    public const decimal DefaultGoodsThreshold = 50_000m;

    /// <summary>Работы и услуги — до двадцати тысяч (п. 10.1.2).</summary>
    public const decimal DefaultWorksThreshold = 20_000m;

    public const string GoodsThresholdCode = "NoContractGoodsThreshold";
    public const string WorksThresholdCode = "NoContractWorksThreshold";

    public static ContractDecision Decide(
        ProcurementSubjectKind kind,
        decimal amount,
        bool supplierIsNonResident = false,
        bool hasRecentSimilar = false,
        bool demandedByProcurementHead = false,
        decimal? goodsThreshold = null,
        decimal? worksThreshold = null)
    {
        // П. 10.4: поставщик-нерезидент — договор всегда, какой бы ни была сумма.
        if (supplierIsNonResident)
            return new(true, "Поставщик — нерезидент Кыргызской Республики (п. 10.4 Положения)");

        // П. 10.3: дробление. Та же продукция того же подразделения дважды за два
        // месяца означает одну потребность, разбитую на части, — её консолидируют
        // и заключают договор.
        if (hasRecentSimilar)
            return new(true,
                "Аналогичная закупка подразделения за последние два месяца — " +
                "потребность консолидируется в договор (п. 10.3 Положения)");

        // П. 10.2: руководитель организатора закупок вправе потребовать договор,
        // исходя из рисков и постоянного характера расходов.
        if (demandedByProcurementHead)
            return new(true,
                "Договор затребован руководителем организатора закупок (п. 10.2 Положения)");

        var предел = Предел(kind, goodsThreshold, worksThreshold);

        if (amount > предел)
            return new(true,
                $"Сумма {amount:N2} сом превышает {предел:N0} сом — " +
                $"{ПределТекст(kind)} (п. 10.1 Положения)");

        return new(false,
            $"Договор не требуется: {ПределТекст(kind)} до {предел:N0} сом (п. 10.1 Положения)");
    }

    private static decimal Предел(
        ProcurementSubjectKind kind, decimal? goodsThreshold, decimal? worksThreshold) =>
        ЭтоРаботыИлиУслуги(kind)
            ? worksThreshold ?? DefaultWorksThreshold
            : goodsThreshold ?? DefaultGoodsThreshold;

    /// <summary>
    /// Разновидности товара — хозяйственный, специфичный, с установкой — по
    /// Положению остаются товаром: отдельный порог назван только для работ и услуг.
    /// </summary>
    private static bool ЭтоРаботыИлиУслуги(ProcurementSubjectKind kind) =>
        kind is ProcurementSubjectKind.Works or ProcurementSubjectKind.Services;

    private static string ПределТекст(ProcurementSubjectKind kind) =>
        ЭтоРаботыИлиУслуги(kind) ? "для работ и услуг" : "для товаров";
}
