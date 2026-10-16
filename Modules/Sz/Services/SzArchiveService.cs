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

    /// <summary>Сдать пачку записок в одно дело (СЗ-7), с отчётом по непрошедшим.</summary>
    Task<SzBulkResult> BulkArchiveAsync(SzBulkArchiveRequest req, int actorUserId);

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
    private readonly delosfera_server.Common.Services.IBankClock _clock;

    // Дата банка (CLK-1): даты подшивки/сроков — по календарю Бишкека, не UTC.
    private DateOnly Today => _clock.Today;

    public SzArchiveService(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit,
        ICurrentUserService currentUser, delosfera_server.Common.Services.IBankClock clock)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
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

    public async Task<SzBulkResult> BulkArchiveAsync(SzBulkArchiveRequest req, int actorUserId)
    {
        var ids = req.Ids.Distinct().ToList();

        // Номера прогружаем заранее: если запись не пройдёт, в отчёте нужен её номер,
        // а не голый id — иначе делопроизводителю не понять, какую записку смотреть.
        var regNumbers = await _db.SzDocuments
            .Where(s => ids.Contains(s.Id))
            .Select(s => new {s.Id, s.Document!.RegNumber})
            .ToDictionaryAsync(x => x.Id, x => x.RegNumber);

        var result = new SzBulkResult {Requested = ids.Count};

        // Пачку подшиваем по одной записке: у каждой своя проверка (статус, оригинал
        // на руках, доступ), и одна непрошедшая не должна отменять остальные. Ошибки
        // валидации бросаются до изменения записи, поэтому tracker остаётся чистым.
        foreach (var id in ids)
        {
            try
            {
                await ArchiveAsync(id, new SzArchiveRequest
                {
                    NomenclatureCaseId = req.NomenclatureCaseId,
                    StorageTermId = req.StorageTermId,
                }, actorUserId);
                result.Succeeded++;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
            {
                result.Failed.Add(new SzBulkFailure
                {
                    SzId = id,
                    RegNumber = regNumbers.GetValueOrDefault(id),
                    Message = ex.Message,
                });
            }
        }

        return result;
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

        // Возвращаем в то состояние, из которого записка ушла в дело. Раньше всегда
        // ставилось «Исполнена» — и забракованная или отозванная записка после
        // возврата из архива числилась исполненной, в реестре и в статистике.
        var restored = await StatusBeforeArchiveAsync(doc.Id);
        await _documents.ChangeStatusAsync(doc.Id, restored, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "RestoredFromArchive", actorUserId, new { status = restored });

        return await GetAsync(szId);
    }

    public async Task<SzArchiveResponse> GetAsync(int szId)
    {
        var sz = await LoadAsync(szId);

        // Смотреть карточку хранения может каждый, кто видит записку (согласующий,
        // подписант, адресат, исполнитель): иначе в карточке записки у них красная
        // ошибка 403. Подшивать и возвращать из архива по-прежнему вправе только
        // автор, руководитель подразделения и делопроизводство (EnsureAccessAsync).
        var canView = await SzVisibility.CanViewAsync(
            _db, sz.Id, _currentUser.UserId,
            canSeeAll: CanManageAll,
            canSeeOthersDrafts: _currentUser.HasPermission(PermissionCode.ManageSystemSettings));
        if (!canView)
            throw new UnauthorizedAccessException("Нет доступа к архивному хранению этой записки");

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
    /// Статус, из которого записка ушла в архив, — из журнала смены статусов
    /// (последний переход «→ В архиве»). Сдаются в дело только исполненная,
    /// забракованная и отозванная записки; если журнал ничего такого не знает
    /// (записку перевели в архив вручную до появления журнала), — «Исполнена».
    /// </summary>
    private async Task<string> StatusBeforeArchiveAsync(int documentId)
    {
        var entries = await _documents.GetAuditAsync(documentId);

        foreach (var e in entries.Where(e => e.Action == "StatusChanged" && e.PayloadJson is not null)
                     .OrderByDescending(e => e.At).ThenByDescending(e => e.Id))
        {
            try
            {
                var root = System.Text.Json.JsonDocument.Parse(e.PayloadJson!).RootElement;
                var to = root.TryGetProperty("to", out var t) ? t.GetString() : null;
                if (to != SzStatus.Archived) continue;

                var from = root.TryGetProperty("from", out var f) ? f.GetString() : null;
                return from is SzStatus.Executed or SzStatus.Rejected or SzStatus.Withdrawn
                    ? from
                    : SzStatus.Executed;
            }
            catch (System.Text.Json.JsonException)
            {
                // Сломанная запись журнала не должна мешать возврату из архива.
            }
        }

        return SzStatus.Executed;
    }

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
