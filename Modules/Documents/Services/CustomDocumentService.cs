using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.DTO;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Services;

public interface ICustomDocumentService
{
    Task<List<CustomDocumentDto>> ListAsync(int definitionId);
    Task<CustomDocumentDto> GetAsync(int documentId);
    Task<CustomDocumentDto> CreateAsync(CustomDocumentSaveRequest request, int authorId);
    Task<CustomDocumentDto> UpdateAsync(int documentId, CustomDocumentSaveRequest request, int actorUserId);

    /// <summary>Отправить на согласование по шаблону маршрута, заданному типу (GEN-06).</summary>
    Task<CustomDocumentDto> SubmitAsync(int documentId, int actorUserId);
}

/// <summary>
/// Карточки документов настраиваемых типов (GEN-06).
///
/// Карточка живёт в общей таблице документов: номер, статус, автор, вложения, аудит
/// и маршрут у неё те же, что у встроенных контуров. Отличается только состав полей,
/// и он лежит отдельным json — иначе каждый новый тип требовал бы миграции.
/// </summary>
public class CustomDocumentService : ICustomDocumentService
{
    private const string StatusDraft = "Draft";
    private const string StatusOnApproval = "OnApproval";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DelosferaDbContext _db;
    private readonly IDocumentTypeDefinitionService _definitions;
    private readonly IDocumentService _documents;
    private readonly Workflow.Services.IRouteEngine _routes;
    private readonly ICurrentUserService _currentUser;

    public CustomDocumentService(
        DelosferaDbContext db,
        IDocumentTypeDefinitionService definitions,
        IDocumentService documents,
        Workflow.Services.IRouteEngine routes,
        ICurrentUserService currentUser)
    {
        _db = db;
        _definitions = definitions;
        _documents = documents;
        _routes = routes;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Черновик кастом-документа правит и отправляет на согласование сам его автор:
    /// карточка принадлежит одному человеку, и переписывать или проталкивать чужую
    /// по её {id} со стороны нельзя. Вмешаться может только администратор системы
    /// (<see cref="PermissionCode.ManageSystemSettings"/>).
    /// </summary>
    private void EnsureAuthorOrPrivileged(Document document, int actorUserId, string message)
    {
        if (document.AuthorId == actorUserId) return;
        if (_currentUser.HasPermission(PermissionCode.ManageSystemSettings)) return;
        throw new UnauthorizedAccessException(message);
    }

    public async Task<List<CustomDocumentDto>> ListAsync(int definitionId)
    {
        var rows = await _db.Documents
            .Include(d => d.Author)
            .Include(d => d.Definition)
            .Where(d => d.DefinitionId == definitionId)
            .OrderByDescending(d => d.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<CustomDocumentDto> GetAsync(int documentId) => ToDto(await LoadAsync(documentId));

    public async Task<CustomDocumentDto> CreateAsync(CustomDocumentSaveRequest request, int authorId)
    {
        var definition = await _db.DocumentTypeDefinitions
            .FirstOrDefaultAsync(d => d.Id == request.DefinitionId)
            ?? throw new KeyNotFoundException("Тип документа не найден");

        if (!definition.IsActive)
            throw new InvalidOperationException($"Тип «{definition.TitleRu}» выключен — новые документы по нему не заводятся");

        var title = string.IsNullOrWhiteSpace(request.Title) ? definition.TitleRu : request.Title.Trim();
        var values = await _definitions.ValidateValuesAsync(definition.Id, request.Values);

        var document = await _documents.CreateAsync(DocumentType.Custom, title, authorId, StatusDraft);

        document.DefinitionId = definition.Id;
        document.FieldValues = values;
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(document.Id));
    }

    public async Task<CustomDocumentDto> UpdateAsync(int documentId, CustomDocumentSaveRequest request, int actorUserId)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId)
            ?? throw new KeyNotFoundException("Документ не найден");

        EnsureAuthorOrPrivileged(document, actorUserId, "Править можно только собственный черновик");

        if (document.DefinitionId is not { } definitionId)
            throw new InvalidOperationException("Документ не относится к настраиваемому типу");

        // Отправленный на согласование документ не правится: участники видят одну
        // версию карточки, и менять её под ними значит обесценить их визы.
        if (document.StatusCode != StatusDraft)
            throw new InvalidOperationException("Править можно только черновик");

        if (!string.IsNullOrWhiteSpace(request.Title)) document.Title = request.Title.Trim();
        document.FieldValues = await _definitions.ValidateValuesAsync(definitionId, request.Values);

        await _db.SaveChangesAsync();
        return ToDto(await LoadAsync(documentId));
    }

    public async Task<CustomDocumentDto> SubmitAsync(int documentId, int actorUserId)
    {
        var document = await LoadTrackedAsync(documentId);

        EnsureAuthorOrPrivileged(document, actorUserId, "Отправить на согласование можно только собственный черновик");

        if (document.DefinitionId is not { } definitionId)
            throw new InvalidOperationException("Документ не относится к настраиваемому типу");

        if (document.StatusCode != StatusDraft)
            throw new InvalidOperationException("Документ уже отправлен на согласование");

        var definition = await _db.DocumentTypeDefinitions.FirstAsync(d => d.Id == definitionId);

        if (definition.RouteTemplateId is not { } templateId)
            throw new InvalidOperationException(
                $"Для типа «{definition.TitleRu}» не задан шаблон маршрута — согласовывать нечем");

        // Обязательные поля проверяем повторно: карточку могли завести, когда поле ещё
        // не было обязательным, а на согласование должен уходить полный комплект.
        var values = ParseValues(document.FieldValues);
        await _definitions.ValidateValuesAsync(definitionId, values);

        var instance = await _routes.InstantiateFromTemplateAsync(document.Id, templateId);
        await _routes.StartAsync(instance.Id, actorUserId);

        document.CurrentRouteInstanceId = instance.Id;
        document.StatusCode = StatusOnApproval;
        await _db.SaveChangesAsync();

        return ToDto(await LoadAsync(documentId));
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<Document> LoadAsync(int id) =>
        await _db.Documents
            .Include(d => d.Author)
            .Include(d => d.Definition)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Документ не найден");

    private async Task<Document> LoadTrackedAsync(int id) =>
        await _db.Documents.FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException("Документ не найден");

    private static Dictionary<string, JsonElement> ParseValues(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw, Json) ?? [];

    private static CustomDocumentDto ToDto(Document d) => new()
    {
        Id = d.Id,
        DefinitionId = d.DefinitionId ?? 0,
        TypeTitle = d.Definition?.TitleRu ?? "Документ",
        RegNumber = d.RegNumber,
        Title = d.Title,
        StatusCode = d.StatusCode,
        AuthorName = d.Author?.FullName,
        CreatedAt = d.CreatedAt,
        Values = ParseValues(d.FieldValues),
    };
}
