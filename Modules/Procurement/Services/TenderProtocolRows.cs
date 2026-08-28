using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

/// <summary>
/// Вариант отбора — общий вид конкурсной заявки и коммерческого предложения.
///
/// Это две разные процедуры с разными правилами, но в протокол они ложатся одной
/// сравнительной таблицей, и дальше протоколу всё равно, откуда взялась строка.
/// </summary>
public sealed record ProtocolOption(
    string Key,
    int SupplierId,
    string SupplierTitle,
    string? SupplierInn,
    decimal Price,
    string? Specification,
    string? DeliveryTerms,
    string? PaymentTerms,
    string Conclusion,
    bool IsWinner,
    /// <summary>Прошёл отбор: допущен комиссией либо соответствует требованиям.</summary>
    bool Eligible);

/// <summary>
/// Строки сравнительной таблицы протокола.
///
/// Вынесено из ProtocolService отдельно и без обращений к базе: это чистое
/// преобразование, и проверять его надо без поднятого приложения.
/// </summary>
public static class TenderProtocolRows
{
    /// <summary>
    /// Строки по конкурсу: заявки, их допуск и результат голосования комиссии.
    ///
    /// Голоса печатаются в заключении по каждой заявке — п. 24.2 Положения требует
    /// открытого голосования, а протокол без его результатов неполон.
    /// </summary>
    public static List<ProtocolOption> Build(Tender tender) =>
        tender.Bids
            .OrderBy(b => b.Price)
            .Select(bid => new ProtocolOption(
                Key: $"bid:{bid.Id}",
                SupplierId: bid.SupplierId,
                SupplierTitle: bid.Supplier?.Title ?? "—",
                SupplierInn: bid.Supplier?.Inn,
                Price: bid.Price,
                Specification: bid.Specification,
                DeliveryTerms: null,
                PaymentTerms: null,
                Conclusion: Conclusion(bid),
                IsWinner: bid.IsWinner,
                Eligible: bid.IsAdmitted))
            .ToList();

    /// <summary>Строки по простой закупке: коммерческие предложения.</summary>
    public static List<ProtocolOption> Build(ProposalComparisonDto comparison) =>
        comparison.Proposals
            .OrderBy(p => p.Price)
            .Select(p => new ProtocolOption(
                Key: $"proposal:{p.Id}",
                SupplierId: p.SupplierId,
                SupplierTitle: p.SupplierTitle,
                SupplierInn: p.SupplierInn,
                Price: p.Price,
                Specification: p.Specification,
                DeliveryTerms: p.DeliveryDays is { } d ? $"{d} дн." : null,
                PaymentTerms: p.PaymentTerms,
                Conclusion: Conclusion(p),
                IsWinner: p.IsWinner,
                Eligible: p.MeetsRequirements == true && !p.SupplierBlacklisted))
            .ToList();

    /// <summary>
    /// Заключение по конкурсной заявке.
    ///
    /// Недопущенная заявка остаётся в таблице: факт её поступления фиксируется,
    /// а основание недопуска — то, чем комиссия объясняет своё решение.
    /// </summary>
    private static string Conclusion(TenderBid bid)
    {
        if (!bid.IsAdmitted)
            return $"Не допущена: {bid.RejectionReason ?? "основание не указано"}";

        if (bid.Votes.Count == 0)
            return "Допущена; голосование не проводилось";

        var за = bid.Votes.Count(v => v.Choice == VoteChoice.For);
        var против = bid.Votes.Count(v => v.Choice == VoteChoice.Against);
        var воздержались = bid.Votes.Count(v => v.Choice == VoteChoice.Abstained);

        return $"Допущена; голосовали: за — {за}, против — {против}, воздержались — {воздержались}";
    }

    /// <summary>Заключение инициатора по коммерческому предложению.</summary>
    private static string Conclusion(ProposalDto proposal) => proposal.MeetsRequirements switch
    {
        true => "Соответствует требованиям",
        false => $"Отклонено: {proposal.RejectionReason ?? "основание не указано"}",
        _ => "Заключение не дано",
    };
}
