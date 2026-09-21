namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Неизменяемая запись журнала аудита (GEN-13, NFR-04): фиксирует все значимые
/// действия (создание, изменение, согласование, подписание, смена статуса).
/// Только добавление — без update/delete. Полиморфна по (EntityType, EntityId).
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    /// <summary>Тип сущности, напр. "Document", "RouteInstance", "Resolution".</summary>
    public required string EntityType { get; set; }

    public int EntityId { get; set; }

    /// <summary>Действие, напр. "Created", "StatusChanged", "Approved", "Signed".</summary>
    public required string Action { get; set; }

    public int? UserId { get; set; }

    public DateTime At { get; set; }

    /// <summary>Доп. данные действия (JSON: старое/новое значение и т.п.).</summary>
    public string? PayloadJson { get; set; }

    // ── Цепочка целостности (AUD-1, tamper-evident) ───────────────────────────

    /// <summary>Хеш предыдущей записи цепи. null — первая запись (или дочейновое легаси до бэкфилла).</summary>
    public string? PrevHash { get; set; }

    /// <summary>SHA-256 этой записи: hash(prevHash + существенные поля). Ретро-правка/удаление рвёт цепь.</summary>
    public string? Hash { get; set; }
}
