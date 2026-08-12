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
        return rows.Select(Map).ToList();
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
