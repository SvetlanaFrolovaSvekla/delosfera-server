using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Настраиваемые параметры контура закупок (PRC-05, PRC-10): балансовая стоимость активов,
/// ЧСК, порог обязательного протокола. Хранятся записями справочника, а не константами —
/// баланс меняется ежеквартально, а порог протокола остаётся открытым вопросом В-4
/// (в процессе — 50 000 сом, в Положении простая закупка определена до 100 000).
/// </summary>
public class ProcurementParameter : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Код параметра: BalanceAssets, Nsk, ProtocolThreshold.</summary>
    public required string Code { get; set; }

    public required string TitleRu { get; set; }

    public decimal Value { get; set; }

    /// <summary>Единица для подписи в интерфейсе: «сом».</summary>
    public required string Unit { get; set; }

    /// <summary>Откуда взято значение — чтобы при приёмке было видно основание.</summary>
    public string? SourceNote { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
