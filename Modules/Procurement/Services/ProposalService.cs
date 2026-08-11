using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProposalService
{
    Task<ProposalComparisonDto> GetComparisonAsync(int requestId);
    Task<ProposalComparisonDto> AddAsync(int requestId, ProposalCreateRequest request, int actorUserId);
    Task<ProposalComparisonDto> SetVerdictAsync(int proposalId, ProposalVerdictRequest request, int actorUserId);
    Task<ProposalComparisonDto> DeleteAsync(int proposalId, int actorUserId);
    Task<ProposalComparisonDto> DeclareWinnerAsync(int requestId, int proposalId, int actorUserId);
}

/// <summary>
/// Коммерческие предложения и сравнительная таблица (PRC-09/11/12).
///
/// Победителем становится предложение с наименьшей ценой среди тех, что прошли
/// проверку технических требований, — так это определено Положением. Поставщики
/// из чёрного списка к отбору не допускаются (PRC-17), поэтому их предложения
/// не участвуют в расчёте минимальной цены.
/// </summary>
public class ProposalService : IProposalService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;

    public ProposalService(DelosferaDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ProposalComparisonDto> GetComparisonAsync(int requestId) =>
        await BuildAsync(await LoadRequestAsync(requestId));

    public async Task<ProposalComparisonDto> AddAsync(
        int requestId, ProposalCreateRequest request, int actorUserId)
    {
        var procurement = await LoadRequestAsync(requestId);

        if (request.Price <= 0)
            throw new ArgumentException("Укажите цену предложения");

        var supplier = await ResolveSupplierAsync(request);

        if (supplier.IsBlacklisted)
            throw new InvalidOperationException(
                $"Поставщик «{supplier.Title}» в чёрном списке недобросовестных поставщиков — к отбору не допускается");

        if (await _db.CommercialProposals.AnyAsync(p => p.RequestId == requestId && p.SupplierId == supplier.Id))
            throw new InvalidOperationException(
                $"Предложение поставщика «{supplier.Title}» уже зарегистрировано в этой закупке");

        var proposal = new CommercialProposal
        {
            RequestId = requestId,
            SupplierId = supplier.Id,
            Price = request.Price,
            DeliveryDays = request.DeliveryDays,
            WarrantyMonths = request.WarrantyMonths,
            PaymentTerms = request.PaymentTerms?.Trim(),
            Specification = request.Specification?.Trim(),
            ReceivedOn = request.ReceivedOn ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };

        _db.CommercialProposals.Add(proposal);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", requestId, "ProposalRegistered", actorUserId, new
        {
            supplier = supplier.Title,
            proposal.Price,
        });

        return await BuildAsync(procurement);
    }

    public async Task<ProposalComparisonDto> SetVerdictAsync(
        int proposalId, ProposalVerdictRequest request, int actorUserId)
    {
        var proposal = await _db.CommercialProposals
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == proposalId)
            ?? throw new KeyNotFoundException("Коммерческое предложение не найдено");

        if (!request.MeetsRequirements && string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new ArgumentException("Укажите причину отклонения предложения — она печатается в протоколе");

        proposal.MeetsRequirements = request.MeetsRequirements;
        proposal.RejectionReason = request.MeetsRequirements ? null : request.RejectionReason!.Trim();

        // Отклонённое предложение не может оставаться победителем.
        if (!request.MeetsRequirements)
            proposal.IsWinner = false;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", proposal.RequestId, "ProposalVerdict", actorUserId, new
        {
            supplier = proposal.Supplier?.Title,
            request.MeetsRequirements,
            proposal.RejectionReason,
        });

        return await BuildAsync(await LoadRequestAsync(proposal.RequestId));
    }

    public async Task<ProposalComparisonDto> DeleteAsync(int proposalId, int actorUserId)
    {
        var proposal = await _db.CommercialProposals
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == proposalId)
            ?? throw new KeyNotFoundException("Коммерческое предложение не найдено");

        var requestId = proposal.RequestId;
        _db.CommercialProposals.Remove(proposal);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", requestId, "ProposalRemoved", actorUserId, new
        {
            supplier = proposal.Supplier?.Title,
        });

        return await BuildAsync(await LoadRequestAsync(requestId));
    }

    public async Task<ProposalComparisonDto> DeclareWinnerAsync(int requestId, int proposalId, int actorUserId)
    {
        var procurement = await LoadRequestAsync(requestId);
        var comparison = await BuildAsync(procurement);

        if (comparison.Blockers.Count > 0)
            throw new InvalidOperationException(string.Join("; ", comparison.Blockers));

        var proposals = await _db.CommercialProposals
            .Include(p => p.Supplier)
            .Where(p => p.RequestId == requestId)
            .ToListAsync();

        var winner = proposals.FirstOrDefault(p => p.Id == proposalId)
                     ?? throw new KeyNotFoundException("Предложение не найдено в этой закупке");

        if (winner.MeetsRequirements != true)
            throw new InvalidOperationException(
                "Победителем может стать только предложение, прошедшее проверку технических требований");

        if (winner.Supplier!.IsBlacklisted)
            throw new InvalidOperationException("Поставщик в чёрном списке — к отбору не допускается");

        // Отклонение от минимальной цены допустимо, но требует записи основания
        // в протоколе: Положение выбирает по цене среди технически подходящих.
        var reasonedChoice = comparison.LowestPrice is { } lowest && winner.Price > lowest;

        foreach (var p in proposals)
            p.IsWinner = p.Id == winner.Id;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", requestId, "WinnerDeclared", actorUserId, new
        {
            supplier = winner.Supplier.Title,
            winner.Price,
            aboveLowestPrice = reasonedChoice,
        });

        var result = await BuildAsync(procurement);
        if (reasonedChoice)
            result.Blockers.Add("Победитель не с наименьшей ценой — основание выбора обязательно указать в протоколе (PRC-12)");

        return result;
    }

    private async Task<ProcurementRequest> LoadRequestAsync(int id) =>
        await _db.ProcurementRequests
            .Include(r => r.Method)
            .Include(r => r.Document)
            .FirstOrDefaultAsync(r => r.Id == id)
        ?? throw new KeyNotFoundException($"Заявка на закупку {id} не найдена");

    private async Task<Supplier> ResolveSupplierAsync(ProposalCreateRequest request)
    {
        if (request.SupplierId is { } id)
            return await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id)
                   ?? throw new KeyNotFoundException("Поставщик не найден");

        var title = request.SupplierTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Укажите поставщика");

        var inn = request.SupplierInn?.Trim();

        // Поставщик заводится один раз: ИНН — ключ, по которому его находят
        // повторные закупки, чёрный список и проверки аффилированности.
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

    private async Task<ProposalComparisonDto> BuildAsync(ProcurementRequest request)
    {
        var proposals = await _db.CommercialProposals
            .Include(p => p.Supplier)
            .Where(p => p.RequestId == request.Id)
            .OrderBy(p => p.Price)
            .ToListAsync();

        // Допущенные — не отклонённые по технике и не из чёрного списка;
        // именно среди них ищется наименьшая цена.
        var eligible = proposals
            .Where(p => p.MeetsRequirements != false && !p.Supplier!.IsBlacklisted)
            .ToList();

        var lowest = eligible.Count > 0 ? eligible.Min(p => p.Price) : (decimal?)null;

        var dto = new ProposalComparisonDto
        {
            RequestId = request.Id,
            MethodTitle = request.Method!.TitleRu,
            MinProposals = request.Method.MinProposals,
            ReceivedCount = proposals.Count,
            EligibleCount = eligible.Count,
            ProtocolRequired = request.ProtocolRequired,
            LowestPrice = lowest,
            Proposals = proposals.Select(p => new ProposalDto
            {
                Id = p.Id,
                SupplierId = p.SupplierId,
                SupplierTitle = p.Supplier!.Title,
                SupplierInn = p.Supplier.Inn,
                Price = p.Price,
                DeliveryDays = p.DeliveryDays,
                WarrantyMonths = p.WarrantyMonths,
                PaymentTerms = p.PaymentTerms,
                Specification = p.Specification,
                ReceivedOn = p.ReceivedOn,
                MeetsRequirements = p.MeetsRequirements,
                RejectionReason = p.RejectionReason,
                IsWinner = p.IsWinner,
                // Отклонённые и поставщики из ЧС в отборе не участвуют: сравнивать их
                // цену с минимальной допущенной бессмысленно и вводит в заблуждение.
                PriceDeltaPercent = lowest is > 0 && p.MeetsRequirements != false && !p.Supplier.IsBlacklisted
                    ? Math.Round((p.Price - lowest.Value) / lowest.Value * 100m, 1)
                    : null,
                SupplierBlacklisted = p.Supplier.IsBlacklisted,
                SupplierAffiliated = p.Supplier.IsAffiliated,
                SupplierReliable = p.Supplier.IsReliable,
            }).ToList(),
        };

        dto.RecommendedProposalId = eligible
            .Where(p => p.MeetsRequirements == true)
            .OrderBy(p => p.Price)
            .Select(p => (int?)p.Id)
            .FirstOrDefault();

        FillBlockers(dto, request);
        return dto;
    }

    private static void FillBlockers(ProposalComparisonDto dto, ProcurementRequest request)
    {
        if (request.Method!.MinProposals > 0 && dto.ReceivedCount < request.Method.MinProposals)
            dto.Blockers.Add(
                $"Зарегистрировано {dto.ReceivedCount} из {request.Method.MinProposals} предложений, требуемых способом «{request.Method.TitleRu}»");

        var undecided = dto.Proposals.Count(p => p.MeetsRequirements is null && !p.SupplierBlacklisted);
        if (undecided > 0)
            dto.Blockers.Add($"По {undecided} предложениям нет заключения о соответствии техническим требованиям (PRC-11)");

        if (dto.EligibleCount == 0 && dto.ReceivedCount > 0)
            dto.Blockers.Add("Ни одно предложение не допущено к отбору");
    }
}
