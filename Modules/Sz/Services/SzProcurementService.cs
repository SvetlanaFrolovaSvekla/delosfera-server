using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzProcurementService
{
    /// <summary>Состояние передачи записки в закупочный контур.</summary>
    Task<SzProcurementResponse> GetAsync(int szId);

    /// <summary>
    /// Запустить закупку по согласованной записке (PRC-01): завести заявку
    /// в едином реестре и связать её с запиской.
    /// </summary>
    Task<SzProcurementResponse> HandOverAsync(int szId, SzProcurementHandoffRequest req, int actorUserId);
}

public class SzProcurementService : ISzProcurementService
{
    /// <summary>Тип связи «записка → заявка на закупку» (GEN-05).</summary>
    public const string LinkType = "SzToProcurement";

    /// <summary>
    /// Статус заявки-заготовки. Собственную статусную модель закупок задаст модуль
    /// Procurement; до его появления заявка лежит черновиком.
    /// </summary>
    private const string ProcurementDraftStatus = "Draft";

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;

    public SzProcurementService(DelosferaDbContext db, IDocumentService documents, IAuditService audit)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
    }

    public async Task<SzProcurementResponse> GetAsync(int szId) => await BuildAsync(await LoadAsync(szId));

    public async Task<SzProcurementResponse> HandOverAsync(
        int szId, SzProcurementHandoffRequest req, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        var state = await BuildAsync(sz);

        if (state.IsHandedOver)
            throw new InvalidOperationException(
                $"Закупка по этой записке уже запущена ({state.ProcurementTitle})");

        if (state.Blockers.Count > 0)
            throw new InvalidOperationException(string.Join("; ", state.Blockers));

        var title = string.IsNullOrWhiteSpace(req.Subject)
            ? $"Закупка по записке {sz.Document!.RegNumber}"
            : req.Subject.Trim();

        // Заявка заводится документом единого реестра: связи, аудит и нумерация уже
        // работают для всех типов, а закупочный контур подхватит её, когда появится.
        var procurement = await _documents.CreateAsync(
            DocumentType.Procurement, title, actorUserId, ProcurementDraftStatus);

        _db.DocumentLinks.Add(new DocumentLink
        {
            FromDocumentId = sz.DocumentId,
            ToDocumentId = procurement.Id,
            LinkType = LinkType
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "HandedToProcurement", actorUserId,
            new { procurementDocumentId = procurement.Id, amount = sz.Amount, hasBudget = sz.HasBudget, note = req.Note });

        return await BuildAsync(await LoadAsync(szId));
    }

    // --- вспомогательное ---

    private async Task<SzProcurementResponse> BuildAsync(SzDocument sz)
    {
        var doc = sz.Document!;
        var isProcurementKind = sz.Kind?.FormKey == SzFormKey.Procurement;

        var link = await _db.DocumentLinks.AsNoTracking()
            .Include(l => l.ToDocument)
            .FirstOrDefaultAsync(l => l.FromDocumentId == sz.DocumentId && l.LinkType == LinkType);

        var response = new SzProcurementResponse
        {
            SzId = sz.Id,
            SzRegNumber = doc.RegNumber,
            IsProcurementKind = isProcurementKind,
            HasBudget = sz.HasBudget,
            Amount = sz.Amount,
            InitiatorName = doc.Author?.FullName,
            InitiatorUnit = sz.AuthorUnit?.TitleRu,
            DueDate = sz.DueDate,
            IsHandedOver = link != null,
            HandedOverAt = link?.CreatedAt,
            ProcurementDocumentId = link?.ToDocumentId,
            ProcurementRegNumber = link?.ToDocument?.RegNumber,
            ProcurementTitle = link?.ToDocument?.Title,
            ProcurementStatus = link?.ToDocument?.StatusCode
        };

        if (link != null) return response;

        // Причины отказа собираем списком: одной ошибкой пользователь узнал бы
        // только про первое препятствие и правил бы их по одному.
        if (!isProcurementKind)
            response.Blockers.Add("Закупка запускается по записке вида «на закупку»");

        if (doc.StatusCode is not (SzStatus.OnExecution or SzStatus.Executed))
            response.Blockers.Add("Записка должна быть согласована и передана на исполнение");

        if (sz.Amount is not > 0)
            response.Blockers.Add("Укажите сумму закупки");

        return response;
    }

    private async Task<SzDocument> LoadAsync(int szId) =>
        await _db.SzDocuments
            .Include(x => x.Document).ThenInclude(d => d!.Author)
            .Include(x => x.Kind)
            .Include(x => x.AuthorUnit)
            .FirstOrDefaultAsync(x => x.Id == szId)
        ?? throw new KeyNotFoundException("Служебная записка не найдена");
}
