using System.Text.Json;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

public class AuditService : IAuditService
{
    private readonly DelosferaDbContext _db;

    public AuditService(DelosferaDbContext db) => _db = db;

    public async Task LogAsync(string entityType, int entityId, string action, int? userId, object? payload = null)
    {
        _db.AuditEntries.Add(new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            At = DateTime.UtcNow,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
        });
        await _db.SaveChangesAsync();
    }
}
