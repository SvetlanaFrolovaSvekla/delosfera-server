using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IGuaranteeService
{
    Task<List<GuaranteeDto>> ListAsync(int? tenderId, int? contractId, bool? activeOnly);
    Task<GuaranteeDto> CreateAsync(GuaranteeCreateRequest request, int actorUserId);
    Task<GuaranteeDto> ReturnAsync(int id, GuaranteeReturnRequest request, int actorUserId);
}

/// <summary>
/// Гарантийные обеспечения ГОКЗ и ГОИД (PRC-20).
///
/// Возврат фиксируется датой и основанием: обеспечение либо возвращается в срок,
/// либо удерживается, и в обоих случаях должно быть видно, почему. Просрочка
/// возврата считается по сроку действия — банк не должен держать чужие деньги
/// дольше, чем обязался.
/// </summary>
public class GuaranteeService : IGuaranteeService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public GuaranteeService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<GuaranteeDto>> ListAsync(int? tenderId, int? contractId, bool? activeOnly)
    {
        var q = Query();

        if (tenderId is { } t)
            q = q.Where(g => g.TenderId == t);

        if (contractId is { } c)
            q = q.Where(g => g.ContractId == c);

        if (activeOnly == true)
            q = q.Where(g => g.ReturnedOn == null && !g.IsForfeited);

        var rows = await q.OrderBy(g => g.ValidUntil).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<GuaranteeDto> CreateAsync(GuaranteeCreateRequest request, int actorUserId)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Укажите сумму обеспечения");

        // Обеспечение всегда к чему-то относится: ГОКЗ — к конкурсной заявке,
        // ГОИД — к договору. Без привязки его нельзя ни вернуть, ни удержать по основанию.
        if (request.Kind == GuaranteeKind.BidSecurity && request.TenderId is null)
            throw new ArgumentException("ГОКЗ оформляется по конкурсу — укажите конкурс");

        if (request.Kind == GuaranteeKind.PerformanceSecurity && request.ContractId is null)
            throw new ArgumentException("ГОИД оформляется по договору — укажите договор");

        var receivedOn = request.ReceivedOn ?? _clock.Today;

        if (request.ValidUntil <= receivedOn)
            throw new ArgumentException("Срок действия обеспечения должен быть позже даты получения");

        var supplier = await ResolveSupplierAsync(request);

        var guarantee = new Guarantee
        {
            Kind = request.Kind,
            Form = request.Form,
            TenderId = request.TenderId,
            ContractId = request.ContractId,
            SupplierId = supplier.Id,
            Amount = request.Amount,
            ReceivedOn = receivedOn,
            ValidUntil = request.ValidUntil,
            DocumentRef = request.DocumentRef?.Trim(),
        };

        _db.Guarantees.Add(guarantee);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Guarantee", guarantee.Id, "Received", actorUserId, new
        {
            kind = KindTitle(request.Kind),
            supplier = supplier.Title,
            request.Amount,
            request.ValidUntil,
        });

        return Map(await Query().FirstAsync(g => g.Id == guarantee.Id));
    }

    public async Task<GuaranteeDto> ReturnAsync(int id, GuaranteeReturnRequest request, int actorUserId)
    {
        var guarantee = await Query().FirstOrDefaultAsync(g => g.Id == id)
                        ?? throw new KeyNotFoundException("Обеспечение не найдено");

        if (guarantee.ReturnedOn is not null || guarantee.IsForfeited)
            throw new InvalidOperationException("Обеспечение уже возвращено или удержано");

        if (request.Forfeit && string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Укажите основание удержания обеспечения");

        if (request.Forfeit)
        {
            guarantee.IsForfeited = true;
        }
        else
        {
            guarantee.ReturnedOn = _clock.Today;
            guarantee.ReturnedByUserId = actorUserId;
        }

        guarantee.Note = request.Note?.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Guarantee", id, request.Forfeit ? "Forfeited" : "Returned", actorUserId, new
        {
            guarantee.Amount,
            request.Note,
        });

        return Map(await Query().FirstAsync(g => g.Id == id));
    }

    private IQueryable<Guarantee> Query() =>
        _db.Guarantees
            .Include(g => g.Supplier)
            .Include(g => g.Tender)
            .Include(g => g.Contract).ThenInclude(c => c!.Document)
            .Include(g => g.ReturnedBy);

    private async Task<Supplier> ResolveSupplierAsync(GuaranteeCreateRequest request)
    {
        if (request.SupplierId is { } id)
            return await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id)
                   ?? throw new KeyNotFoundException("Поставщик не найден");

        var title = request.SupplierTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Укажите поставщика");

        var inn = request.SupplierInn?.Trim();

        var existing = inn is not null
            ? await _db.Suppliers.FirstOrDefaultAsync(s => s.Inn == inn)
            : await _db.Suppliers.FirstOrDefaultAsync(s => s.Title == title);

        if (existing is not null)
            return existing;

        var supplier = new Supplier {Title = title, Inn = inn};
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return supplier;
    }

    private GuaranteeDto Map(Guarantee g)
    {
        var today = _clock.Today;
        var open = g.ReturnedOn is null && !g.IsForfeited;

        return new GuaranteeDto
        {
            Id = g.Id,
            Kind = g.Kind,
            KindTitle = KindTitle(g.Kind),
            Form = g.Form,
            FormTitle = g.Form == GuaranteeForm.BankGuarantee ? "Банковская гарантия" : "Денежные средства",
            TenderId = g.TenderId,
            TenderRegNumber = g.Tender?.RegNumber,
            ContractId = g.ContractId,
            ContractRegNumber = g.Contract?.Document?.RegNumber,
            SupplierId = g.SupplierId,
            SupplierTitle = g.Supplier?.Title ?? "—",
            Amount = g.Amount,
            ReceivedOn = g.ReceivedOn,
            ValidUntil = g.ValidUntil,
            DocumentRef = g.DocumentRef,
            ReturnedOn = g.ReturnedOn,
            ReturnedBy = g.ReturnedBy?.FullName,
            IsForfeited = g.IsForfeited,
            Note = g.Note,
            IsReturnOverdue = open && g.ValidUntil < today,
            DaysLeft = g.ValidUntil.DayNumber - today.DayNumber,
        };
    }

    private static string KindTitle(GuaranteeKind kind) => kind switch
    {
        GuaranteeKind.PerformanceSecurity => "ГОИД — обеспечение исполнения договора",
        _ => "ГОКЗ — обеспечение конкурсной заявки",
    };
}
