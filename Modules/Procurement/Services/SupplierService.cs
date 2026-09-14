using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface ISupplierService
{
    Task<List<SupplierDto>> ListAsync(string? query, bool? blacklistedOnly);
    Task<SupplierDto> UpsertAsync(SupplierUpsertRequest request, int actorUserId);
    Task<SupplierDto> BlacklistAsync(int id, BlacklistRequest request, int actorUserId);
    Task<SupplierDto> RemoveFromBlacklistAsync(int id, int actorUserId);
    Task<SupplierDto> SetReliabilityAsync(int id, ReliabilityRequest request, int actorUserId);

    /// <summary>Оценки работы поставщика со средним баллом (ЗК-9).</summary>
    Task<SupplierRatingsResponse> ListRatingsAsync(int supplierId);

    /// <summary>Поставить оценку поставщику (ЗК-9).</summary>
    Task<SupplierRatingDto> AddRatingAsync(int supplierId, AddSupplierRatingRequest request, int actorUserId);
}

/// <summary>
/// Поставщики и чёрный список недобросовестных (PRC-07/17).
///
/// Чёрный список ведётся сроком, а не флагом «навсегда»: ограничение по Положению
/// накладывается на период, и по его истечении поставщик снова допускается к отбору
/// без ручной чистки реестра.
/// </summary>
public class SupplierService : ISupplierService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public SupplierService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<SupplierDto>> ListAsync(string? query, bool? blacklistedOnly)
    {
        var q = _db.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            q = q.Where(s => EF.Functions.ILike(s.Title, term)
                             || (s.Inn != null && EF.Functions.ILike(s.Inn, term)));
        }

        if (blacklistedOnly == true)
            q = q.Where(s => s.IsBlacklisted);

        var rows = await q.OrderBy(s => s.Title).ToListAsync();
        var dtos = rows.Select(Map).ToList();

        // Средний балл одним сгруппированным запросом, а не по строке на поставщика:
        // реестр показывает рейтинг сразу у всех.
        var ids = rows.Select(s => s.Id).ToList();
        var agg = await _db.SupplierRatings
            .Where(r => ids.Contains(r.SupplierId))
            .GroupBy(r => r.SupplierId)
            .Select(g => new {SupplierId = g.Key, Avg = g.Average(x => (double)x.Score), Count = g.Count()})
            .ToDictionaryAsync(x => x.SupplierId, x => x);

        foreach (var d in dtos)
        {
            if (!agg.TryGetValue(d.Id, out var a)) continue;
            d.AverageRating = Math.Round(a.Avg, 1);
            d.RatingCount = a.Count;
        }

        return dtos;
    }

    public async Task<SupplierRatingsResponse> ListRatingsAsync(int supplierId)
    {
        var ratings = await _db.SupplierRatings
            .Where(r => r.SupplierId == supplierId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var authorIds = ratings.Select(r => r.AuthorUserId).Distinct().ToList();
        var names = await _db.Users
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var items = ratings.Select(r => new SupplierRatingDto
        {
            Id = r.Id,
            AuthorUserId = r.AuthorUserId,
            AuthorName = names.GetValueOrDefault(r.AuthorUserId),
            ContractId = r.ContractId,
            Score = r.Score,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
        }).ToList();

        return new SupplierRatingsResponse
        {
            Count = items.Count,
            Average = items.Count > 0 ? Math.Round(items.Average(i => (double)i.Score), 1) : null,
            Items = items,
        };
    }

    public async Task<SupplierRatingDto> AddRatingAsync(int supplierId, AddSupplierRatingRequest request, int actorUserId)
    {
        if (request.Score < 1 || request.Score > 5)
            throw new ArgumentException("Оценка должна быть от 1 до 5");

        var supplier = await LoadAsync(supplierId);

        if (request.ContractId is { } contractId
            && !await _db.Set<ProcurementContract>().AnyAsync(c => c.Id == contractId))
            throw new ArgumentException("Договор не найден");

        var rating = new SupplierRating
        {
            SupplierId = supplier.Id,
            AuthorUserId = actorUserId,
            ContractId = request.ContractId,
            Score = request.Score,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.SupplierRatings.Add(rating);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Supplier", supplier.Id, "Rated", actorUserId, new {rating.Score, rating.ContractId});

        var authorName = await _db.Users
            .Where(u => u.Id == actorUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();

        return new SupplierRatingDto
        {
            Id = rating.Id,
            AuthorUserId = rating.AuthorUserId,
            AuthorName = authorName,
            ContractId = rating.ContractId,
            Score = rating.Score,
            Comment = rating.Comment,
            CreatedAt = rating.CreatedAt,
        };
    }

    public async Task<SupplierDto> UpsertAsync(SupplierUpsertRequest request, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Укажите наименование поставщика");

        var supplier = request.Id is { } id
            ? await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id)
              ?? throw new KeyNotFoundException("Поставщик не найден")
            : new Supplier {Title = request.Title.Trim()};

        supplier.Title = request.Title.Trim();
        supplier.Inn = request.Inn?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.DirectorName = request.DirectorName?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.IsAffiliated = request.IsAffiliated;

        if (request.Id is null)
            _db.Suppliers.Add(supplier);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Supplier", supplier.Id, request.Id is null ? "Created" : "Updated", actorUserId, null);

        return Map(supplier);
    }

    public async Task<SupplierDto> BlacklistAsync(int id, BlacklistRequest request, int actorUserId)
    {
        var supplier = await LoadAsync(id);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Укажите обоснование включения в чёрный список (приложение №4)");

        if (request.Until is { } until && until <= _clock.Today)
            throw new ArgumentException("Срок ограничения должен быть в будущем");

        supplier.IsBlacklisted = true;
        supplier.BlacklistReason = request.Reason.Trim();
        supplier.BlacklistedUntil = request.Until;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Supplier", supplier.Id, "Blacklisted", actorUserId, new
        {
            supplier.Title,
            request.Reason,
            request.Until,
        });

        return Map(supplier);
    }

    public async Task<SupplierDto> RemoveFromBlacklistAsync(int id, int actorUserId)
    {
        var supplier = await LoadAsync(id);

        if (!supplier.IsBlacklisted)
            throw new InvalidOperationException("Поставщик не в чёрном списке");

        supplier.IsBlacklisted = false;
        supplier.BlacklistedUntil = null;

        // Обоснование не стирается: журнал должен объяснять, за что поставщик
        // был ограничен и когда ограничение снято.
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Supplier", supplier.Id, "RemovedFromBlacklist", actorUserId, new
        {
            supplier.Title,
            previousReason = supplier.BlacklistReason,
        });

        return Map(supplier);
    }

    public async Task<SupplierDto> SetReliabilityAsync(int id, ReliabilityRequest request, int actorUserId)
    {
        var supplier = await LoadAsync(id);

        supplier.IsReliable = request.IsReliable;
        supplier.ReliabilityCheckedOn = _clock.Today;
        supplier.HasTaxClearance = request.HasTaxClearance;
        supplier.HasSocialFundClearance = request.HasSocialFundClearance;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Supplier", supplier.Id, "ReliabilityChecked", actorUserId, new
        {
            supplier.Title,
            request.IsReliable,
            request.HasTaxClearance,
            request.HasSocialFundClearance,
        });

        return Map(supplier);
    }

    private async Task<Supplier> LoadAsync(int id) =>
        await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id)
        ?? throw new KeyNotFoundException("Поставщик не найден");

    private SupplierDto Map(Supplier s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Inn = s.Inn,
        Address = s.Address,
        DirectorName = s.DirectorName,
        Phone = s.Phone,
        Email = s.Email,
        IsAffiliated = s.IsAffiliated,
        IsReliable = s.IsReliable,
        ReliabilityCheckedOn = s.ReliabilityCheckedOn,
        HasTaxClearance = s.HasTaxClearance,
        HasSocialFundClearance = s.HasSocialFundClearance,
        IsBlacklisted = s.IsBlacklisted,
        BlacklistReason = s.BlacklistReason,
        BlacklistedUntil = s.BlacklistedUntil,
        // Срок истёк — ограничение больше не действует, даже если запись осталась.
        BlacklistExpired = s.IsBlacklisted && s.BlacklistedUntil is { } until && until < _clock.Today,
    };
}
