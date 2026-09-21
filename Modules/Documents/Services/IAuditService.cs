namespace delosfera_server.Modules.Documents.Services;

/// <summary>Итог проверки целостности хеш-цепи аудита (AUD-1).</summary>
public sealed record AuditChainStatus(bool Valid, int CheckedCount, long? BrokenAtId, string? Reason)
{
    public static AuditChainStatus Ok(int checkedCount) => new(true, checkedCount, null, null);
    public static AuditChainStatus Broken(long brokenAtId, string reason) => new(false, 0, brokenAtId, reason);
}

/// <summary>
/// Журнал аудита (GEN-13): только добавление записей о значимых действиях. Неизменяемость
/// доказуема хеш-цепью (AUD-1) — см. VerifyChainAsync.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string entityType, int entityId, string action, int? userId, object? payload = null);

    /// <summary>Проверить целостность хеш-цепи аудита (AUD-1).</summary>
    Task<AuditChainStatus> VerifyChainAsync(CancellationToken ct = default);

    /// <summary>Однократно достроить цепь по легаси-записям без хеша (AUD-1).</summary>
    Task<int> BackfillChainAsync(CancellationToken ct = default);
}
