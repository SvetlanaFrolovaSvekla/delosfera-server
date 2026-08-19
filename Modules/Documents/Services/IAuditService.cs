namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Журнал аудита (GEN-13): только добавление записей о значимых действиях.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string entityType, int entityId, string action, int? userId, object? payload = null);
}
