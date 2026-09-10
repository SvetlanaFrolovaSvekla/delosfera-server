using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzArchiveService
{
    /// <summary>Подшить записку в дело номенклатуры и перевести в архив (SZ-07, GEN-09).</summary>
    Task<SzArchiveResponse> ArchiveAsync(int szId, SzArchiveRequest req, int actorUserId);

    /// <summary>Вернуть записку из архива: подшили не в то дело либо документ понадобился в работе.</summary>
    Task<SzArchiveResponse> RestoreAsync(int szId, int actorUserId);

    /// <summary>Карточка архивного хранения записки.</summary>
    Task<SzArchiveResponse> GetAsync(int szId);

    /// <summary>Опись дела: что подшито и до какого года хранится.</summary>
    Task<List<SzArchiveResponse>> CaseInventoryAsync(int caseId);
}

public class SzArchiveService : ISzArchiveService
{
    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public SzArchiveService(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _currentUser = currentUser;
    }

    // Подшивка в дело — делопроизводственная операция: право «регистрировать записки»
    // или «видеть все записки» снимает привязку к подразделению. Прочим доступна
    // только своя записка (автор) и записки своего подразделения (руководитель).
    private bool CanManageAll =>
        _currentUser.HasPermission(PermissionCode.RegisterSz)
        || _currentUser.HasPermission(PermissionCode.ViewAllSz);

    private async Task EnsureAccessAsync(SzDocument sz)
    {
        if (!await SzVisibility.CanAccessAsync(_db, sz, _currentUser.UserId, CanManageAll))
            throw new UnauthorizedAccessException("Нет доступа к архивному хранению этой записки");
    }

    public async Task<SzArchiveResponse> ArchiveAsync(int szId, SzArchiveRequest req, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);
        var doc = sz.Document!;

        // В дело подшивают документ, работа по которому закончена.
        if (doc.StatusCode is not (SzStatus.Executed or SzStatus.Rejected or SzStatus.Withdrawn))
            throw new InvalidOperationException(
                "В архив сдаётся исполненная, забракованная или отозванная записка");

        // Бумажный документ нельзя подшить, пока оригинал не вернулся в дело.
        if (doc.IsPaperCarrier && sz.OriginalHandedAt != null && sz.OriginalReturnedAt == null)
            throw new InvalidOperationException(
                $"Оригинал на руках у {sz.OriginalHolderUser?.FullName ?? "получателя"} — сначала примите возврат");

        var nomenclatureCase = await _db.NomenclatureCases
            .Include(c => c.StorageTerm)
            .FirstOrDefaultAsync(c => c.Id == req.NomenclatureCaseId)
            ?? throw new KeyNotFoundException("Дело номенклатуры не найдено");

        if (!nomenclatureCase.IsActive)
            throw new InvalidOperationException("Дело закрыто для подшивки");

        // Срок берётся из дела, но его можно переопределить: отдельные записки хранятся дольше.
        var termId = req.StorageTermId ?? nomenclatureCase.StorageTermId
            ?? throw new InvalidOperationException(
                "У дела не задан срок хранения — укажите его явно");

        var term = await _db.StorageTerms.FirstOrDefaultAsync(t => t.Id == termId)
            ?? throw new KeyNotFoundException("Срок хранения не найден");

        doc.NomenclatureCaseId = nomenclatureCase.Id;
        doc.StorageTermId = term.Id;
        doc.ArchivedOn = Today;
        doc.DestroyAfterYear = CalculateDestroyAfterYear(term, nomenclatureCase);

        await _documents.ChangeStatusAsync(doc.Id, SzStatus.Archived, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "Archived", actorUserId,
            new { caseIndex = nomenclatureCase.Index, term = term.Code, destroyAfter = doc.DestroyAfterYear });

        return await GetAsync(szId);
    }

    public async Task<SzArchiveResponse> RestoreAsync(int szId, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);
        var doc = sz.Document!;

        if (doc.StatusCode != SzStatus.Archived)
            throw new InvalidOperationException("Записка не в архиве");

        doc.NomenclatureCaseId = null;
        doc.StorageTermId = null;
        doc.ArchivedOn = null;
        doc.DestroyAfterYear = null;

        // Возвращаем в то состояние, из которого записка ушла в дело: исполненную —
        // в исполненные. Точный прежний статус не храним, поэтому берём «Исполнена»
        // как единственное состояние, из которого документ мог попасть в дело штатно.
        await _documents.ChangeStatusAsync(doc.Id, SzStatus.Executed, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "RestoredFromArchive", actorUserId);

        return await GetAsync(szId);
    }

    public async Task<SzArchiveResponse> GetAsync(int szId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);
        return Map(sz);
    }

    public async Task<List<SzArchiveResponse>> CaseInventoryAsync(int caseId)
    {
        // Опись дела — целиком делопроизводственный документ: он смешивает записки
        // разных подразделений, поэтому доступен только делопроизводству.
        if (!CanManageAll)
            throw new UnauthorizedAccessException("Опись дела доступна делопроизводству");

        var items = await BaseQuery()
            .Where(x => x.Document!.NomenclatureCaseId == caseId)
            .OrderBy(x => x.Document!.RegNumber)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    // --- вспомогательное ---

    /// <summary>
    /// Срок хранения отсчитывается от года закрытия дела (делопроизводственная практика),
    /// а пока дело открыто — считать не от чего. При постоянном хранении года нет вовсе.
    /// </summary>
    private static int? CalculateDestroyAfterYear(StorageTerm term, NomenclatureCase caseItem)
    {
        if (term.Years is not int years) return null;
        if (caseItem.ClosedOn is not DateOnly closed) return null;

        return closed.Year + years;
    }

    private IQueryable<SzDocument> BaseQuery() => _db.SzDocuments.AsNoTracking()
        .Include(x => x.Document).ThenInclude(d => d!.NomenclatureCase)
        .Include(x => x.Document).ThenInclude(d => d!.StorageTerm);

    private async Task<SzDocument> LoadAsync(int szId) =>
        await _db.SzDocuments
            .Include(x => x.Document).ThenInclude(d => d!.NomenclatureCase)
            .Include(x => x.Document).ThenInclude(d => d!.StorageTerm)
            .Include(x => x.OriginalHolderUser)
            .FirstOrDefaultAsync(x => x.Id == szId)
        ?? throw new KeyNotFoundException("Служебная записка не найдена");

    private static SzArchiveResponse Map(SzDocument sz)
    {
        var doc = sz.Document;
        return new SzArchiveResponse
        {
            SzId = sz.Id,
            RegNumber = doc?.RegNumber,
            Title = doc?.Title,
            StatusCode = doc?.StatusCode ?? SzStatus.Draft,
            IsArchived = doc?.StatusCode == SzStatus.Archived,
            ArchivedOn = doc?.ArchivedOn,
            NomenclatureCaseId = doc?.NomenclatureCaseId,
            CaseIndex = doc?.NomenclatureCase?.Index,
            CaseTitle = doc?.NomenclatureCase?.TitleRu,
            CaseClosedOn = doc?.NomenclatureCase?.ClosedOn,
            StorageTermId = doc?.StorageTermId,
            StorageTerm = doc?.StorageTerm?.TitleRu,
            StorageYears = doc?.StorageTerm?.Years,
            DestroyAfterYear = doc?.DestroyAfterYear,
            // Пока дело открыто, год уничтожения неизвестен — об этом нужно сказать прямо.
            DestroyYearPending = doc?.StorageTermId != null
                                 && doc.StorageTerm?.Years != null
                                 && doc.DestroyAfterYear == null
        };
    }
}
