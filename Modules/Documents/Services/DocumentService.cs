using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

public class DocumentService : IDocumentService
{
    private const string EntityName = "Document";

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly INumeratorService _numerator;

    public DocumentService(DelosferaDbContext db, IAuditService audit, INumeratorService numerator)
    {
        _db = db;
        _audit = audit;
        _numerator = numerator;
    }

    public async Task<Document> CreateAsync(DocumentType type, string title, int authorId, string statusCode)
    {
        var doc = new Document
        {
            Type = type,
            Title = title,
            AuthorId = authorId,
            StatusCode = statusCode
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(EntityName, doc.Id, "Created", authorId, new { type = type.ToString(), title });
        return doc;
    }

    public Task<Document?> GetAsync(int id) =>
        _db.Documents
            .AsNoTracking()
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task ChangeStatusAsync(int id, string newStatus, int? userId)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Документ id={id} не найден");

        var from = doc.StatusCode;
        if (from == newStatus) return;

        doc.StatusCode = newStatus;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(EntityName, id, "StatusChanged", userId, new { from, to = newStatus });
    }

    public async Task<string> RegisterAsync(
        int id, string scope, string scopeKey, string pattern, int? userId,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Документ id={id} не найден");

        if (!string.IsNullOrEmpty(doc.RegNumber))
            return doc.RegNumber; // уже зарегистрирован

        var regNumber = await _numerator.NextAsync(doc.Type, scope, scopeKey, pattern, tokens);
        doc.RegNumber = regNumber;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(EntityName, id, "Registered", userId, new { regNumber });
        return regNumber;
    }

    public Task<List<AuditEntry>> GetAuditAsync(int id) =>
        _db.AuditEntries
            .AsNoTracking()
            .Where(x => x.EntityType == EntityName && x.EntityId == id)
            .OrderBy(x => x.At)
            .ToListAsync();
}
