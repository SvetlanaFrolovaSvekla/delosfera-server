using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Dictionaries.Services;

public class OrganizationUnitService : IOrganizationUnitService
{

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBankClock _clock;

    public OrganizationUnitService(
        DelosferaDbContext db, ICurrentUserService currentUser, IBankClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<OrganizationUnitResponse>> GetAllAsync(OrganizationUnitSortBy sortBy, string? search,
        string languageCode)
    {
        IQueryable<OrganizationUnit> query = _db.OrganizationUnits
            .Include(x => x.HeadUser)
            .Include(x => x.CuratorUser);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleRu, $"%{term}%") ||
                (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{term}%")) ||
                (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{term}%")));
        }

        query = sortBy switch
        {
            OrganizationUnitSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            OrganizationUnitSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            OrganizationUnitSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            OrganizationUnitSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<OrganizationUnitResponse> CreateAsync(CreateOrganizationUnitRequest request, string languageCode)
    {
        if (request.ParentId.HasValue)
        {
            await HierarchyValidation.EnsureParentExistsAsync(_db.OrganizationUnits, request.ParentId.Value,
                pid => $"Родительское подразделение с id={pid} не найдено");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.OrganizationUnits, request.ParentId.Value,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        if (request.HeadUserId.HasValue)
            await EnsureUserExistsAsync(request.HeadUserId.Value, "Начальник");
        if (request.CuratorUserId.HasValue)
            await EnsureUserExistsAsync(request.CuratorUserId.Value, "Куратор");

        var entity = new OrganizationUnit
        {
            TitleRu = request.TitleRu, TitleEn = request.TitleEn, TitleKg = request.TitleKg,
            ParentId = request.ParentId, HeadUserId = request.HeadUserId, CuratorUserId = request.CuratorUserId
        };

        _db.OrganizationUnits.Add(entity);
        await _db.SaveChangesAsync();

        _db.OrganizationUnitHistory.Add(new OrganizationUnitHistory
        {
            OrgUnitId = entity.Id,
            Kind = OrgUnitChangeKind.Created,
            NewValue = entity.TitleRu,
            EffectiveFrom = _clock.Today,
            ChangedByUserId = _currentUser.UserId == 0 ? null : _currentUser.UserId,
            At = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        return await LoadResponseAsync(entity.Id, languageCode);
    }

    public async Task<OrganizationUnitResponse> UpdateAsync(int id, UpdateOrganizationUnitRequest request,
        string languageCode)
    {
        var entity = await _db.OrganizationUnits.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Структурное подразделение с id={id} не найдено");

        // Подразделение пришло из портала — значит портал им и распоряжается.
        // Название, место в структуре, начальника и куратора он перезаписывает
        // при каждом проходе, и правка здесь дожила бы до ближайшей ночи.
        //
        // Молча принять её было бы хуже отказа: человек увидел бы сохранённое
        // значение, ушёл, а наутро оно вернулось бы к прежнему — и связать одно
        // с другим уже никто не смог бы.
        //
        // Переводы и признак бумажной записки портал не присылает: они наши,
        // и править их можно.
        if (entity.ExternalId is not null)
        {
            var занято = new List<string>();

            if (!string.Equals(request.TitleRu?.Trim(), entity.TitleRu?.Trim(), StringComparison.Ordinal))
                занято.Add("название");
            if (request.ParentId != entity.ParentId)
                занято.Add("вышестоящее подразделение");
            if (request.HeadUserId != entity.HeadUserId)
                занято.Add("начальник");
            if (request.CuratorUserId != entity.CuratorUserId)
                занято.Add("куратор");

            if (занято.Count > 0)
                throw new InvalidOperationException(
                    $"Это подразделение ведётся в портале банка, здесь его копия. " +
                    $"Изменить нельзя: {string.Join(", ", занято)}. " +
                    "Правка не сохранилась бы — ближайшая синхронизация вернула бы значение из портала. " +
                    "Меняйте в портале; здесь можно править только переводы названия.");
        }

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Подразделение не может быть родителем самого себя");

            await HierarchyValidation.EnsureParentExistsAsync(_db.OrganizationUnits, request.ParentId.Value,
                pid => $"Родительское подразделение с id={pid} не найдено");
            await HierarchyValidation.EnsureNoCircularReferenceAsync(_db.OrganizationUnits, id, request.ParentId.Value,
                "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.OrganizationUnits, request.ParentId.Value,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        if (request.HeadUserId.HasValue)
            await EnsureUserExistsAsync(request.HeadUserId.Value, "Начальник");
        if (request.CuratorUserId.HasValue)
            await EnsureUserExistsAsync(request.CuratorUserId.Value, "Куратор");

        // Историю пишем до присвоения: после него старые значения уже не достать,
        // а именно они и нужны в отчёте «что было на дату» (GEN-08).
        await RecordChangesAsync(entity, request);

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.ParentId = request.ParentId;
        entity.HeadUserId = request.HeadUserId;
        entity.CuratorUserId = request.CuratorUserId;
        await _db.SaveChangesAsync();

        return await LoadResponseAsync(id, languageCode);
    }

    public async Task<List<OrgUnitHistoryResponse>> GetHistoryAsync(int id)
    {
        var rows = await _db.OrganizationUnitHistory
            .Include(x => x.ChangedByUser)
            .Where(x => x.OrgUnitId == id)
            .OrderByDescending(x => x.At)
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(x => new OrgUnitHistoryResponse
        {
            Id = x.Id,
            Kind = x.Kind,
            KindTitle = KindTitle(x.Kind),
            OldValue = x.OldValue,
            NewValue = x.NewValue,
            Reason = x.Reason,
            EffectiveFrom = x.EffectiveFrom,
            ChangedByName = x.ChangedByUser?.FullName,
            At = x.At,
        }).ToList();
    }

    public async Task<List<OrgUnitSnapshotResponse>> GetSnapshotAsync(DateOnly date, string languageCode)
    {
        var units = await _db.OrganizationUnits
            .Include(x => x.HeadUser)
            .Include(x => x.CuratorUser)
            .Include(x => x.Parent)
            .AsNoTracking()
            .ToListAsync();

        // Изменения ПОСЛЕ запрошенной даты отматываем назад: текущее состояние известно,
        // а история хранит, чем оно было до каждой правки.
        var later = await _db.OrganizationUnitHistory
            .Where(x => x.EffectiveFrom > date)
            .OrderByDescending(x => x.At)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<OrgUnitSnapshotResponse>();

        foreach (var unit in units)
        {
            var snapshot = new OrgUnitSnapshotResponse
            {
                Id = unit.Id,
                Title = unit.ResolveTitle(languageCode),
                ParentTitle = unit.Parent?.TitleRu,
                HeadName = unit.HeadUser?.FullName,
                CuratorName = unit.CuratorUser?.FullName,
            };

            foreach (var change in later.Where(x => x.OrgUnitId == unit.Id))
            {
                switch (change.Kind)
                {
                    case OrgUnitChangeKind.Created:
                        snapshot.CreatedLater = true;
                        break;
                    case OrgUnitChangeKind.Renamed:
                        snapshot.Title = change.OldValue ?? snapshot.Title;
                        break;
                    case OrgUnitChangeKind.Moved:
                        snapshot.ParentTitle = change.OldValue;
                        break;
                    case OrgUnitChangeKind.HeadChanged:
                        snapshot.HeadName = change.OldValue;
                        break;
                    case OrgUnitChangeKind.CuratorChanged:
                        snapshot.CuratorName = change.OldValue;
                        break;
                }
            }

            result.Add(snapshot);
        }

        return result.OrderBy(x => x.Title).ToList();
    }

    private static string KindTitle(OrgUnitChangeKind kind) => kind switch
    {
        OrgUnitChangeKind.Created => "Подразделение заведено",
        OrgUnitChangeKind.Renamed => "Переименование",
        OrgUnitChangeKind.Moved => "Переподчинение",
        OrgUnitChangeKind.HeadChanged => "Смена руководителя",
        OrgUnitChangeKind.CuratorChanged => "Смена куратора",
        OrgUnitChangeKind.AttributesChanged => "Изменение реквизитов",
        OrgUnitChangeKind.Removed => "Подразделение упразднено",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Фиксирует изменения реквизитов подразделения (GEN-08). Каждое изменение — своя
    /// запись: переименование и переподчинение произошли по разным основаниям, и в
    /// одной строке они неразличимы.
    /// </summary>
    private async Task RecordChangesAsync(OrganizationUnit entity, UpdateOrganizationUnitRequest request)
    {
        var changes = new List<(OrgUnitChangeKind Kind, string? Old, string? New)>();

        if (!string.Equals(entity.TitleRu, request.TitleRu, StringComparison.Ordinal))
            changes.Add((OrgUnitChangeKind.Renamed, entity.TitleRu, request.TitleRu));

        if (entity.ParentId != request.ParentId)
            changes.Add((OrgUnitChangeKind.Moved,
                await UnitTitleAsync(entity.ParentId), await UnitTitleAsync(request.ParentId)));

        if (entity.HeadUserId != request.HeadUserId)
            changes.Add((OrgUnitChangeKind.HeadChanged,
                await UserNameAsync(entity.HeadUserId), await UserNameAsync(request.HeadUserId)));

        if (entity.CuratorUserId != request.CuratorUserId)
            changes.Add((OrgUnitChangeKind.CuratorChanged,
                await UserNameAsync(entity.CuratorUserId), await UserNameAsync(request.CuratorUserId)));

        if (changes.Count == 0) return;

        var now = DateTime.UtcNow;
        var today = _clock.Today;
        var actor = _currentUser.UserId;

        foreach (var (kind, oldValue, newValue) in changes)
        {
            _db.OrganizationUnitHistory.Add(new OrganizationUnitHistory
            {
                OrgUnitId = entity.Id,
                Kind = kind,
                OldValue = oldValue,
                NewValue = newValue,
                EffectiveFrom = today,
                ChangedByUserId = actor == 0 ? null : actor,
                At = now,
            });
        }
    }

    private async Task<string?> UnitTitleAsync(int? unitId) =>
        unitId is null
            ? null
            : await _db.OrganizationUnits.Where(x => x.Id == unitId).Select(x => x.TitleRu).FirstOrDefaultAsync();

    private async Task<string?> UserNameAsync(int? userId) =>
        userId is null
            ? null
            : await _db.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstOrDefaultAsync();

    private async Task EnsureUserExistsAsync(int userId, string role)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId);
        if (!exists) throw new KeyNotFoundException($"{role} с id={userId} не найден среди пользователей");
    }

    private async Task<OrganizationUnitResponse> LoadResponseAsync(int id, string languageCode)
    {
        var entity = await _db.OrganizationUnits
            .Include(x => x.HeadUser)
            .Include(x => x.CuratorUser)
            .FirstAsync(x => x.Id == id);
        return ToResponse(entity, languageCode);
    }


    public async Task DeleteAsync(int id)
    {
        var entity = await _db.OrganizationUnits.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Структурное подразделение с id={id} не найдено");

        // Удалять пришедшее из портала бессмысленно вдвойне: ближайший проход
        // заведёт его заново, и получится то же подразделение с новым
        // идентификатором — а привязанные к прежнему документы останутся
        // висеть на удалённом.
        if (entity.ExternalId is not null)
            throw new InvalidOperationException(
                "Это подразделение ведётся в портале банка. Удалить его здесь нельзя: " +
                "ближайшая синхронизация заведёт его заново, а документы остались бы " +
                "привязанными к удалённой записи. Расформировывайте в портале.");

        var hasChildren = await _db.OrganizationUnits.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException(
                "Нельзя удалить подразделение — у него есть дочерние записи. Сначала удалите или перенесите их");

        _db.OrganizationUnits.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить подразделение — на него есть ссылки в других документах");
        }
    }

    private static OrganizationUnitResponse ToResponse(OrganizationUnit entity, string languageCode) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        ParentId = entity.ParentId,
        HeadUserId = entity.HeadUserId,
        HeadUserName = entity.HeadUser?.FullName,
        CuratorUserId = entity.CuratorUserId,
        CuratorUserName = entity.CuratorUser?.FullName,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}