using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.PowerOfAttorney.DTO;
using delosfera_server.Modules.PowerOfAttorney.Models;

namespace delosfera_server.Modules.PowerOfAttorney.Services;

public interface IPoaService
{
    Task<PoaDto> CreateAsync(PoaSaveRequest request, int currentUserId, CancellationToken ct = default);
    Task<PoaDto> UpdateAsync(int id, PoaSaveRequest request, CancellationToken ct = default);
    Task<PoaDto> GetAsync(int id, CancellationToken ct = default);
    Task<PoaListResult> SearchAsync(PoaFilterRequest filter, CancellationToken ct = default);

    Task<PoaDto> IssueAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<PoaDto> RevokeAsync(int id, string reason, DateOnly? on, int currentUserId, CancellationToken ct = default);

    Task<List<PoaDto>> ValidForUserAsync(int userId, DateOnly onDay, CancellationToken ct = default);
    Task<List<PoaDto>> ExpiringAsync(int days, CancellationToken ct = default);

    /// <summary>Приложить скан доверенности.</summary>
    Task<PoaFileDto> AddFileAsync(int poaId, IFormFile file, int actorUserId, CancellationToken ct = default);

    Task<List<PoaFileDto>> FilesAsync(int poaId, CancellationToken ct = default);
}

/// <summary>
/// Реестр доверенностей.
///
/// Отвечает на два вопроса, ради которых он и заводится: вправе ли человек подписать
/// это сегодня, и что банк выдал и не забрал обратно. Всё остальное — обвязка.
/// </summary>
public class PoaService : IPoaService
{
    private readonly DelosferaDbContext _db;

    private readonly Files.Services.IFileStorageService _storage;

    private readonly IAuditService _audit;

    private readonly INumeratorService _numerator;

    public PoaService(DelosferaDbContext db, Files.Services.IFileStorageService storage, IAuditService audit, INumeratorService numerator)
    {
        _db = db;
        _storage = storage;
        _audit = audit;
        _numerator = numerator;
    }

    /// <summary>
    /// Приложить скан доверенности.
    ///
    /// Доверенность действует бумажным подлинником, и реестр без скана отвечает
    /// на вопрос «вправе ли он подписать» одними реквизитами. Модель файла
    /// существовала, но приложить его было нечем: ни одной точки загрузки.
    /// </summary>
    public async Task<PoaFileDto> AddFileAsync(
        int poaId, IFormFile file, int actorUserId, CancellationToken ct = default)
    {
        var poa = await _db.PowersOfAttorney.FirstOrDefaultAsync(p => p.Id == poaId, ct)
                  ?? throw new KeyNotFoundException("Доверенность не найдена");

        var stored = await _storage.SaveAsync(file, actorUserId, ct);

        var link = new Models.PoaFile
        {
            PowerOfAttorneyId = poa.Id,
            FileId = stored.Id,
            UploadedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.PoaFiles.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("PowerOfAttorney", poa.Id, "FileAdded", actorUserId,
            new { fileId = stored.Id, fileName = stored.OriginalFileName });

        return new PoaFileDto
        {
            Id = link.Id,
            FileId = stored.Id,
            FileName = stored.OriginalFileName,
            SizeBytes = stored.SizeBytes,
            UploadedAt = link.CreatedAt,
        };
    }

    public async Task<List<PoaFileDto>> FilesAsync(int poaId, CancellationToken ct = default) =>
        await _db.PoaFiles
            .AsNoTracking()
            .Where(f => f.PowerOfAttorneyId == poaId)
            .OrderBy(f => f.Id)
            .Select(f => new PoaFileDto
            {
                Id = f.Id,
                FileId = f.FileId,
                FileName = f.File!.OriginalFileName,
                SizeBytes = f.File.SizeBytes,
                UploadedAt = f.CreatedAt,
            })
            .ToListAsync(ct);

    public async Task<PoaDto> CreateAsync(PoaSaveRequest request, int currentUserId, CancellationToken ct = default)
    {
        Validate(request);

        var now = DateTime.UtcNow;

        var poa = new Models.PowerOfAttorney
        {
            Year = request.IssuedOn.Year,
            IssuedOn = request.IssuedOn,
            GrantorUserId = request.GrantorUserId,
            ParentPoaId = request.ParentPoaId,
            HolderKind = request.HolderKind,
            HolderUserId = request.HolderKind == PoaHolderKind.Employee ? request.HolderUserId : null,
            HolderName = request.HolderName.Trim(),
            HolderPosition = Trim(request.HolderPosition),
            HolderUnitId = request.HolderUnitId,
            HolderIdentityDocument = Trim(request.HolderIdentityDocument),
            Powers = request.Powers.Trim(),
            AllowsDelegation = request.AllowsDelegation,
            AmountLimit = request.AmountLimit,
            AmountCurrency = Trim(request.AmountCurrency),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            OriginalLocation = Trim(request.OriginalLocation),
            Status = PoaStatus.Draft,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await CheckParentAsync(poa, ct);

        _db.PowersOfAttorney.Add(poa);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("PowerOfAttorney", poa.Id, "Created", currentUserId,
            new { holder = poa.HolderName, validFrom = poa.ValidFrom, validTo = poa.ValidTo });

        return await GetAsync(poa.Id, ct);
    }

    public async Task<PoaDto> UpdateAsync(int id, PoaSaveRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var poa = await LoadAsync(id, ct);

        // Подписанную доверенность не правят. Её текст уже у третьих лиц, и правка
        // задним числом означала бы, что реестр показывает не то, что предъявляют.
        if (poa.Status is PoaStatus.Active or PoaStatus.Revoked or PoaStatus.Expired)
            throw new InvalidOperationException(
                "Выданную доверенность изменить нельзя — её нужно отозвать и выдать новую.");

        poa.IssuedOn = request.IssuedOn;
        poa.Year = request.IssuedOn.Year;
        poa.GrantorUserId = request.GrantorUserId;
        poa.ParentPoaId = request.ParentPoaId;
        poa.HolderKind = request.HolderKind;
        poa.HolderUserId = request.HolderKind == PoaHolderKind.Employee ? request.HolderUserId : null;
        poa.HolderName = request.HolderName.Trim();
        poa.HolderPosition = Trim(request.HolderPosition);
        poa.HolderUnitId = request.HolderUnitId;
        poa.HolderIdentityDocument = Trim(request.HolderIdentityDocument);
        poa.Powers = request.Powers.Trim();
        poa.AllowsDelegation = request.AllowsDelegation;
        poa.AmountLimit = request.AmountLimit;
        poa.AmountCurrency = Trim(request.AmountCurrency);
        poa.ValidFrom = request.ValidFrom;
        poa.ValidTo = request.ValidTo;
        poa.OriginalLocation = Trim(request.OriginalLocation);
        poa.UpdatedAt = DateTime.UtcNow;

        await CheckParentAsync(poa, ct);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>Выдать: присвоить номер по книге и перевести в действующие.</summary>
    public async Task<PoaDto> IssueAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var poa = await LoadAsync(id, ct);

        if (poa.Status == PoaStatus.Active)
            throw new InvalidOperationException("Доверенность уже выдана.");

        if (poa.Status is PoaStatus.Revoked or PoaStatus.Expired)
            throw new InvalidOperationException("Доверенность закрыта — выдать её заново нельзя.");

        poa.RegNumber ??= await NextNumberAsync(poa.Year, ct);
        poa.Status = PoaStatus.Active;
        poa.SignedAt = DateTime.UtcNow;
        poa.SignedByUserId = currentUserId;
        poa.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("PowerOfAttorney", poa.Id, "Issued", currentUserId,
            new { number = poa.RegNumber, holder = poa.HolderName, validFrom = poa.ValidFrom, validTo = poa.ValidTo });

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Отозвать. Причина обязательна: отзыв затрагивает третьих лиц, и вопрос
    /// «почему полномочие прекращено» задают именно им.
    /// </summary>
    public async Task<PoaDto> RevokeAsync(
        int id, string reason, DateOnly? on, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите причину отзыва.");

        var poa = await LoadAsync(id, ct);

        if (poa.Status != PoaStatus.Active)
            throw new InvalidOperationException("Отозвать можно только действующую доверенность.");

        poa.Status = PoaStatus.Revoked;
        poa.RevokedOn = on ?? DateOnly.FromDateTime(DateTime.UtcNow);
        poa.RevokeReason = reason.Trim();
        poa.RevokedByUserId = currentUserId;
        poa.UpdatedAt = DateTime.UtcNow;

        // Передоверия держатся на родительской доверенности: отпало полномочие
        // доверителя — отпали и выданные им. Оставить их действующими значило бы
        // сохранить полномочие, которого больше нет.
        var children = await _db.PowersOfAttorney
            .Where(c => c.ParentPoaId == id && c.Status == PoaStatus.Active)
            .ToListAsync(ct);

        foreach (var child in children)
        {
            child.Status = PoaStatus.Revoked;
            child.RevokedOn = poa.RevokedOn;
            child.RevokeReason = $"Отозвана основная доверенность № {poa.RegNumber}: {poa.RevokeReason}";
            child.RevokedByUserId = currentUserId;
            child.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("PowerOfAttorney", poa.Id, "Revoked", currentUserId,
            new { number = poa.RegNumber, reason = poa.RevokeReason, revokedOn = poa.RevokedOn });

        foreach (var child in children)
        {
            await _audit.LogAsync("PowerOfAttorney", child.Id, "Revoked", currentUserId,
                new { number = child.RegNumber, reason = child.RevokeReason, revokedOn = child.RevokedOn, parentPoaId = id });
        }

        return await GetAsync(id, ct);
    }

    public async Task<PoaDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await Query().FirstOrDefaultAsync(p => p.Id == id, ct);
        return dto ?? throw new KeyNotFoundException("Доверенность не найдена.");
    }

    public async Task<PoaListResult> SearchAsync(PoaFilterRequest filter, CancellationToken ct = default)
    {
        var query = _db.PowersOfAttorney.AsNoTracking().AsQueryable();

        if (filter.Statuses is {Count: > 0})
            query = query.Where(p => filter.Statuses.Contains(p.Status));

        if (filter.HolderUserId is {} holder)
            query = query.Where(p => p.HolderUserId == holder);

        if (filter.GrantorUserId is {} grantor)
            query = query.Where(p => p.GrantorUserId == grantor);

        if (filter.UnitId is {} unit)
            query = query.Where(p => p.HolderUnitId == unit);

        if (filter.ValidOn is {} day)
            query = query.Where(p =>
                p.Status == PoaStatus.Active
                && p.ValidFrom <= day && p.ValidTo >= day
                && (p.RevokedOn == null || p.RevokedOn > day));

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.HolderName, $"%{text}%")
                || EF.Functions.ILike(p.Powers, $"%{text}%")
                || (p.RegNumber != null && EF.Functions.ILike(p.RegNumber, $"%{text}%")));
        }

        var total = await query.CountAsync(ct);

        var page = Math.Max(filter.Page, 1);
        var size = Math.Clamp(filter.PageSize, 1, 200);

        var items = await Project(query
                .OrderByDescending(p => p.IssuedOn).ThenByDescending(p => p.Id)
                .Skip((page - 1) * size)
                .Take(size))
            .ToListAsync(ct);

        return new PoaListResult {Total = total, Page = page, PageSize = size, Items = items};
    }

    /// <summary>
    /// Чем этот человек вправе распоряжаться в указанный день. Именно тот вопрос,
    /// который задают перед подписанием договора.
    /// </summary>
    public async Task<List<PoaDto>> ValidForUserAsync(int userId, DateOnly onDay, CancellationToken ct = default) =>
        await Project(_db.PowersOfAttorney.AsNoTracking()
                .Where(p => p.HolderUserId == userId
                            && p.Status == PoaStatus.Active
                            && p.ValidFrom <= onDay && p.ValidTo >= onDay
                            && (p.RevokedOn == null || p.RevokedOn > onDay))
                .OrderBy(p => p.ValidTo))
            .ToListAsync(ct);

    /// <summary>Что истекает в ближайшие дни — чтобы продлить заранее, а не задним числом.</summary>
    public async Task<List<PoaDto>> ExpiringAsync(int days, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var edge = today.AddDays(Math.Clamp(days, 1, 365));

        return await Project(_db.PowersOfAttorney.AsNoTracking()
                .Where(p => p.Status == PoaStatus.Active && p.ValidTo >= today && p.ValidTo <= edge)
                .OrderBy(p => p.ValidTo))
            .ToListAsync(ct);
    }

    // ── внутреннее ──────────────────────────────────────────────

    private IQueryable<PoaDto> Query() => Project(_db.PowersOfAttorney.AsNoTracking());

    private static IQueryable<PoaDto> Project(IQueryable<Models.PowerOfAttorney> query) =>
        query.Select(p => new PoaDto
        {
            Id = p.Id,
            RegNumber = p.RegNumber,
            IssuedOn = p.IssuedOn,
            Status = p.Status.ToString(),
            GrantorUserId = p.GrantorUserId,
            GrantorName = p.GrantorUser == null ? null : p.GrantorUser.FullName,
            ParentPoaId = p.ParentPoaId,
            ParentRegNumber = p.ParentPoa == null ? null : p.ParentPoa.RegNumber,
            HolderKind = p.HolderKind.ToString(),
            HolderUserId = p.HolderUserId,
            HolderName = p.HolderName,
            HolderPosition = p.HolderPosition,
            HolderUnitId = p.HolderUnitId,
            HolderUnit = p.HolderUnit == null ? null : p.HolderUnit.TitleRu,
            HolderIdentityDocument = p.HolderIdentityDocument,
            Powers = p.Powers,
            AllowsDelegation = p.AllowsDelegation,
            AmountLimit = p.AmountLimit,
            AmountCurrency = p.AmountCurrency,
            ValidFrom = p.ValidFrom,
            ValidTo = p.ValidTo,
            SignedAt = p.SignedAt,
            RevokedOn = p.RevokedOn,
            RevokeReason = p.RevokeReason,
            RevokedBy = p.RevokedByUser == null ? null : p.RevokedByUser.FullName,
            OriginalLocation = p.OriginalLocation,
            OriginalHandedAt = p.OriginalHandedAt,
            OriginalReturnedAt = p.OriginalReturnedAt,
            FileCount = p.Files.Count,
            ChildCount = p.Children.Count,
        });

    private async Task<Models.PowerOfAttorney> LoadAsync(int id, CancellationToken ct) =>
        await _db.PowersOfAttorney.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new KeyNotFoundException("Доверенность не найдена.");

    private static void Validate(PoaSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HolderName))
            throw new InvalidOperationException("Укажите, кому выдаётся доверенность.");

        if (string.IsNullOrWhiteSpace(request.Powers))
            throw new InvalidOperationException("Укажите полномочия по доверенности.");

        if (request.ValidTo < request.ValidFrom)
            throw new InvalidOperationException("Дата окончания раньше даты начала.");

        if (request.HolderKind == PoaHolderKind.Employee && request.HolderUserId is null)
            throw new InvalidOperationException("Для сотрудника выберите учётную запись.");

        if (request.AmountLimit is {} limit && limit <= 0)
            throw new InvalidOperationException("Предельная сумма должна быть больше нуля.");

        if (request.AmountLimit is not null && string.IsNullOrWhiteSpace(request.AmountCurrency))
            throw new InvalidOperationException("Укажите валюту предельной суммы.");
    }

    /// <summary>
    /// Передоверие не может быть шире и дольше основания. Иначе доверенность на
    /// год порождала бы передоверие на три, и полномочие пережило бы источник.
    /// </summary>
    private async Task CheckParentAsync(Models.PowerOfAttorney poa, CancellationToken ct)
    {
        if (poa.ParentPoaId is null) return;

        if (poa.ParentPoaId == poa.Id)
            throw new InvalidOperationException("Доверенность не может быть выдана по самой себе.");

        var parent = await _db.PowersOfAttorney
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == poa.ParentPoaId, ct)
            ?? throw new InvalidOperationException("Основание не найдено.");

        if (!parent.AllowsDelegation)
            throw new InvalidOperationException(
                "Основная доверенность не даёт права передоверия.");

        if (poa.ValidTo > parent.ValidTo)
            throw new InvalidOperationException(
                $"Передоверие не может действовать дольше основания (по {parent.ValidTo:dd.MM.yyyy}).");

        if (parent.AmountLimit is {} parentLimit
            && (poa.AmountLimit is null || poa.AmountLimit > parentLimit))
        {
            throw new InvalidOperationException(
                $"Предельная сумма передоверия не может превышать сумму основания ({parentLimit:N2}).");
        }
    }

    private async Task<string> NextNumberAsync(int year, CancellationToken ct)
    {
        return await _numerator.NextAsync(DocumentType.PowerOfAttorney, "global", year.ToString(), "{seq}/{year}");
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
