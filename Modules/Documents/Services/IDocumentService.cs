using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Documents.Services;

/// <summary>
/// Базовые операции над единой карточкой документа. Контурные модули (СЗ, ВНД,
/// закупки) используют его для регистрации, смены статуса, вложений и аудита.
/// </summary>
public interface IDocumentService
{
    Task<Document> CreateAsync(DocumentType type, string title, int authorId, string statusCode);

    Task<Document?> GetAsync(int id);

    Task ChangeStatusAsync(int id, string newStatus, int? userId);

    /// <summary>Присвоить регистрационный номер через нумератор (GEN-09).</summary>
    Task<string> RegisterAsync(
        int id, string scope, string scopeKey, string pattern, int? userId,
        IReadOnlyDictionary<string, string>? tokens = null);

    /// <summary>Журнал аудита по документу (хронология).</summary>
    Task<List<AuditEntry>> GetAuditAsync(int id);
}
