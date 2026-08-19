using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IClaimService
{
    Task<List<ClaimDto>> ListAsync(int? contractId, bool? openOnly);
    Task<ClaimDto> CreateAsync(int contractId, ClaimCreateRequest request, int actorUserId);
    Task<ClaimDto> SendAsync(int id, ClaimSendRequest request, int actorUserId);
    Task<ClaimDto> AnswerAsync(int id, ClaimAnswerRequest request, int actorUserId);
    Task<ClaimDto> CloseAsync(int id, ClaimCloseRequest request, int actorUserId);
}

/// <summary>
/// Претензионная работа по договору закупки (PRC-21): фиксация нарушения,
/// направление письма и контроль ответа контрагента.
///
/// Срок ответа хранится датой, а не «через сколько дней»: он считается от даты
/// направления и не должен меняться задним числом при правке регламента.
/// </summary>
public class ClaimService : IClaimService
{
    /// <summary>Срок ответа по умолчанию, календарных дней.</summary>
    private const int DefaultResponseDays = 30;

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public ClaimService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<ClaimDto>> ListAsync(int? contractId, bool? openOnly)
    {
        var q = Query();

        if (contractId is { } id)
            q = q.Where(c => c.ContractId == id);

        if (openOnly == true)
            q = q.Where(c => c.Status != ClaimStatus.Satisfied
                             && c.Status != ClaimStatus.Withdrawn);

        var rows = await q.OrderByDescending(c => c.Id).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<ClaimDto> CreateAsync(int contractId, ClaimCreateRequest request, int actorUserId)
    {
        var contract = await _db.ProcurementContracts
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Id == contractId)
            ?? throw new KeyNotFoundException("Договор не найден");

        if (string.IsNullOrWhiteSpace(request.Violation))
            throw new ArgumentException("Опишите нарушение условий договора");

        var claim = new ProcurementClaim
        {
            ContractId = contractId,
            Violation = request.Violation.Trim(),
            Demand = request.Demand?.Trim(),
            Amount = request.Amount,
            RegNumber = await NextNumberAsync(),
        };

        _db.ProcurementClaims.Add(claim);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementClaim", claim.Id, "Created", actorUserId, new
        {
            contractId,
            supplier = contract.Supplier?.Title,
            claim.Violation,
        });

        return Map(await Query().FirstAsync(c => c.Id == claim.Id));
    }

    public async Task<ClaimDto> SendAsync(int id, ClaimSendRequest request, int actorUserId)
    {
        var claim = await Query().FirstOrDefaultAsync(c => c.Id == id)
                    ?? throw new KeyNotFoundException("Претензия не найдена");

        if (claim.Status != ClaimStatus.Draft)
            throw new InvalidOperationException("Направляется только подготовленная претензия");

        if (string.IsNullOrWhiteSpace(claim.Demand))
            throw new InvalidOperationException(
                "Не сформулировано требование к контрагенту — направлять претензию нечем");

        var sentOn = request.SentOn ?? _clock.Today;

        claim.SentOn = sentOn;
        claim.ResponseDeadline = request.ResponseDeadline ?? sentOn.AddDays(DefaultResponseDays);
        claim.Status = ClaimStatus.Sent;

        if (claim.ResponseDeadline <= sentOn)
            throw new ArgumentException("Срок ответа должен быть позже даты направления");

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementClaim", id, "Sent", actorUserId, new
        {
            claim.SentOn,
            claim.ResponseDeadline,
        });

        return Map(await Query().FirstAsync(c => c.Id == id));
    }

    public async Task<ClaimDto> AnswerAsync(int id, ClaimAnswerRequest request, int actorUserId)
    {
        var claim = await Query().FirstOrDefaultAsync(c => c.Id == id)
                    ?? throw new KeyNotFoundException("Претензия не найдена");

        if (claim.Status != ClaimStatus.Sent)
            throw new InvalidOperationException("Ответ фиксируется по направленной претензии");

        if (string.IsNullOrWhiteSpace(request.Response))
            throw new ArgumentException("Укажите содержание ответа контрагента");

        claim.Response = request.Response.Trim();
        claim.AnsweredOn = request.AnsweredOn ?? _clock.Today;
        claim.Status = ClaimStatus.Answered;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementClaim", id, "Answered", actorUserId, new {claim.AnsweredOn});

        return Map(await Query().FirstAsync(c => c.Id == id));
    }

    public async Task<ClaimDto> CloseAsync(int id, ClaimCloseRequest request, int actorUserId)
    {
        var claim = await Query().FirstOrDefaultAsync(c => c.Id == id)
                    ?? throw new KeyNotFoundException("Претензия не найдена");

        if (request.Status is not (ClaimStatus.Satisfied or ClaimStatus.Litigation or ClaimStatus.Withdrawn))
            throw new ArgumentException(
                "Претензия закрывается удовлетворением, передачей в суд или отзывом");

        if (string.IsNullOrWhiteSpace(request.Outcome))
            throw new ArgumentException("Укажите итог претензионной работы");

        claim.Status = request.Status;
        claim.Outcome = request.Outcome.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementClaim", id, "Closed", actorUserId, new
        {
            status = StatusTitle(request.Status),
            request.Outcome,
        });

        return Map(await Query().FirstAsync(c => c.Id == id));
    }

    private IQueryable<ProcurementClaim> Query() =>
        _db.ProcurementClaims
            .Include(c => c.Contract).ThenInclude(x => x!.Document)
            .Include(c => c.Contract).ThenInclude(x => x!.Supplier);

    private async Task<string> NextNumberAsync()
    {
        var year = _clock.Today.Year;
        var prefix = $"ПР-{year}-";

        var last = await _db.ProcurementClaims
            .Where(c => c.RegNumber != null && c.RegNumber.StartsWith(prefix))
            .OrderByDescending(c => c.RegNumber)
            .Select(c => c.RegNumber)
            .FirstOrDefaultAsync();

        var seq = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{seq:D4}";
    }

    private ClaimDto Map(ProcurementClaim c) => new()
    {
        Id = c.Id,
        ContractId = c.ContractId,
        ContractRegNumber = c.Contract?.Document?.RegNumber,
        SupplierTitle = c.Contract?.Supplier?.Title,
        RegNumber = c.RegNumber,
        Status = c.Status,
        StatusTitle = StatusTitle(c.Status),
        Violation = c.Violation,
        Demand = c.Demand,
        Amount = c.Amount,
        SentOn = c.SentOn,
        ResponseDeadline = c.ResponseDeadline,
        AnsweredOn = c.AnsweredOn,
        Response = c.Response,
        Outcome = c.Outcome,
        IsResponseOverdue = c.Status == ClaimStatus.Sent
                            && c.ResponseDeadline is { } deadline
                            && deadline < _clock.Today,
    };

    private static string StatusTitle(ClaimStatus status) => status switch
    {
        ClaimStatus.Sent => "Направлена",
        ClaimStatus.Answered => "Получен ответ",
        ClaimStatus.Satisfied => "Удовлетворена",
        ClaimStatus.Litigation => "Передана в суд",
        ClaimStatus.Withdrawn => "Отозвана",
        _ => "Подготовка",
    };
}
