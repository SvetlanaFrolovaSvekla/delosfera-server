using System.Text.Json;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Авторизация кастом-документов (GEN-06): черновик правит и отправляет на
/// согласование только его автор. IDOR по {id} на чужой черновик закрыт.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CustomDocumentAuthorizationTests
{
    private readonly PostgresFixture _postgres;

    public CustomDocumentAuthorizationTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Update_ByAuthor_Succeeds()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDraftAsync(db);
        var service = NewService(db, new FakeCurrentUser(authorId));

        var result = await service.UpdateAsync(
            documentId, new CustomDocumentSaveRequest {Title = "Новый заголовок"}, actorUserId: authorId);

        Assert.Equal("Новый заголовок", result.Title);
    }

    [Fact]
    public async Task Update_ByNonAuthor_WithoutPrivilege_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDraftAsync(db);
        var service = NewService(db, new FakeCurrentUser(authorId + 1000)); // не автор, без прав

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(
                documentId, new CustomDocumentSaveRequest {Title = "Взлом"}, actorUserId: authorId + 1000));

        // Чужая правка не сохранилась.
        var reloaded = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId);
        Assert.Equal("Черновик автора", reloaded.Title);
    }

    [Fact]
    public async Task Update_ByAdmin_Succeeds()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDraftAsync(db);
        var admin = new FakeCurrentUser(authorId + 1000, PermissionCode.ManageSystemSettings);
        var service = NewService(db, admin);

        var result = await service.UpdateAsync(
            documentId, new CustomDocumentSaveRequest {Title = "Правка администратором"}, actorUserId: authorId + 1000);

        Assert.Equal("Правка администратором", result.Title);
    }

    [Fact]
    public async Task Submit_ByNonAuthor_WithoutPrivilege_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (documentId, authorId) = await SeedDraftAsync(db);
        var service = NewService(db, new FakeCurrentUser(authorId + 1000));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SubmitAsync(documentId, actorUserId: authorId + 1000));

        // Статус не сдвинут чужой попыткой отправки.
        var reloaded = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId);
        Assert.Equal("Draft", reloaded.StatusCode);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static CustomDocumentService NewService(
        DelosferaDbContext db, delosfera_server.Common.Services.Authorization.ICurrentUserService currentUser) =>
        new(db, new FakeDefinitions(), new ThrowingDocuments(), new ThrowingRoutes(), currentUser);

    /// <summary>Черновик кастом-типа: автор, статус Draft, привязка к активному типу.</summary>
    private static async Task<(int DocumentId, int AuthorId)> SeedDraftAsync(DelosferaDbContext db)
    {
        var author = new User
        {
            FullName = "Автор Кастома",
            Email = $"custom-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(author);
        await db.SaveChangesAsync();

        var definition = new DocumentTypeDefinition
        {
            Code = $"CUS{Guid.NewGuid():N}"[..12],
            TitleRu = "Настраиваемый тип",
            IsActive = true,
        };

        db.DocumentTypeDefinitions.Add(definition);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Custom,
            Title = "Черновик автора",
            StatusCode = "Draft",
            AuthorId = author.Id,
            DefinitionId = definition.Id,
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (document.Id, author.Id);
    }

    /// <summary>Валидация значений здесь не проверяется — возвращаем нормализованный пустой набор.</summary>
    private sealed class FakeDefinitions : IDocumentTypeDefinitionService
    {
        public Task<string> ValidateValuesAsync(int definitionId, Dictionary<string, JsonElement> values) =>
            Task.FromResult("{}");

        public Task<List<DocumentTypeDefinitionDto>> ListAsync(bool includeInactive = false) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> GetAsync(int id) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> CreateAsync(DocumentTypeSaveRequest request) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> UpdateAsync(int id, DocumentTypeSaveRequest request) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> AddFieldAsync(int definitionId, DocumentTypeFieldRequest request) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> UpdateFieldAsync(int fieldId, DocumentTypeFieldRequest request) => throw new NotSupportedException();
        public Task<DocumentTypeDefinitionDto> DeleteFieldAsync(int fieldId) => throw new NotSupportedException();
    }

    /// <summary>Операции над карточкой не участвуют в проверяемых путях авторизации.</summary>
    private sealed class ThrowingDocuments : IDocumentService
    {
        public Task<Document> CreateAsync(DocumentType type, string title, int authorId, string statusCode) => throw new NotSupportedException();
        public Task<Document?> GetAsync(int id) => throw new NotSupportedException();
        public Task ChangeStatusAsync(int id, string newStatus, int? userId) => throw new NotSupportedException();
        public Task<string> RegisterAsync(int id, string scope, string scopeKey, string pattern, int? userId, IReadOnlyDictionary<string, string>? tokens = null) => throw new NotSupportedException();
        public Task<List<AuditEntry>> GetAuditAsync(int id) => throw new NotSupportedException();
    }

    /// <summary>Движок маршрута до проверки прав не доходит — все методы бросают.</summary>
    private sealed class ThrowingRoutes : IRouteEngine
    {
        public Task<RouteInstance> InstantiateFromTemplateAsync(int documentId, int templateId, IReadOnlySet<string>? satisfiedConditions = null) => throw new NotSupportedException();
        public Task StartAsync(int routeInstanceId, int actorUserId) => throw new NotSupportedException();
        public Task ResolveAsync(int participantId, ResolutionType type, string? comment, int actorUserId, int? signatureId = null) => throw new NotSupportedException();
        public Task AppendSigningStepAsync(int routeInstanceId, int signerUserId) => throw new NotSupportedException();
        public Task<RouteInstance> InstantiateForSignerAsync(int documentId, int signerUserId, int? timeNormHours = null) => throw new NotSupportedException();
        public Task<RouteInstance> InstantiateForApproversAsync(int documentId, IReadOnlyList<int> approverUserIds, bool parallel, int? timeNormHours = null, int? signerUserId = null) => throw new NotSupportedException();
        public Task ConfirmRemarkResolvedAsync(int remarkId, int actorUserId) => throw new NotSupportedException();
        public Task ApplyOverdueAsync(DateTime now) => throw new NotSupportedException();
        public Task InterruptAsync(int routeInstanceId, int actorUserId) => throw new NotSupportedException();
    }
}
