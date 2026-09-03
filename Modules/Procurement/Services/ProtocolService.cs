using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProtocolService
{
    Task<ProtocolDto?> GetAsync(int requestId);

    /// <summary>
    /// Все протоколы по заявке — по одному на заседание комиссии.
    /// Свежие сверху: обычно нужен последний, а прежние читают ради хода дела.
    /// </summary>
    Task<List<ProtocolDto>> ListAsync(int requestId);
    Task<ProtocolDto> GenerateAsync(int requestId, int actorUserId);
    Task<ProtocolDto> UpdateAsync(int requestId, ProtocolUpdateRequest request, int actorUserId);
    Task<ProtocolDto> SignAsync(int requestId, ProtocolSignRequest request, int actorUserId);
}

/// <summary>
/// Протокол закупки (PRC-10). Формируется из сравнительной таблицы предложений
/// и фиксирует все поступившие варианты, решение и основание выбора.
///
/// Протокол — снимок: строки копируются в него, а не читаются из живых КП.
/// Хеш содержимого связывает подписи с конкретной редакцией: правка предложений
/// после подписания делает протокол устаревшим, а подписи — аннулированными (SIG-01).
/// </summary>
public class ProtocolService : IProtocolService
{
    private const string NumberPattern = "ПЗ-{year}-{seq:D4}";

    private readonly DelosferaDbContext _db;
    private readonly IProposalService _proposals;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public ProtocolService(
        DelosferaDbContext db, IProposalService proposals, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _proposals = proposals;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ProtocolDto?> GetAsync(int requestId)
    {
        var protocol = await LoadAsync(requestId);
        return protocol is null ? null : await BuildAsync(protocol);
    }

    public async Task<List<ProtocolDto>> ListAsync(int requestId)
    {
        var protocols = await ProtocolQuery()
            .Where(p => p.RequestId == requestId)
            .OrderByDescending(p => p.MeetingDate ?? p.ProtocolDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        var result = new List<ProtocolDto>(protocols.Count);
        foreach (var p in protocols) result.Add(await BuildAsync(p));

        return result;
    }

    /// <summary>
    /// Дата заседания, итоги которого оформляются. Берётся у действующего
    /// конкурса; у простой закупки комиссии нет, и протокол остаётся один.
    /// </summary>
    private async Task<DateOnly?> ДатаЗаседанияАsync(int requestId) =>
        await _db.Tenders
            .Where(t => t.RequestId == requestId
                        && t.Status != TenderStatus.Cancelled
                        && t.Status != TenderStatus.Failed)
            .OrderByDescending(t => t.Id)
            .Select(t => t.MeetingDate)
            .FirstOrDefaultAsync();

    public async Task<ProtocolDto> GenerateAsync(int requestId, int actorUserId)
    {
        var request = await _db.ProcurementRequests
            .Include(r => r.Method)
            .Include(r => r.InitiatorUnit)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Заявка на закупку {requestId} не найдена");

        if (!request.ProtocolRequired)
            throw new InvalidOperationException(
                "Протокол закупки не требуется: сумма не превышает установленный порог (PRC-10)");

        var варианты = await ВариантыОтбораАsync(requestId);
        var winner = варианты.FirstOrDefault(v => v.IsWinner)
                     ?? throw new InvalidOperationException(
                         "Протокол формируется после определения победителя закупки");

        // Протокол оформляется на заседание: у конкурса — на то, что назначено,
        // у простой закупки заседаний нет и протокол один.
        var датаЗаседания = await ДатаЗаседанияАsync(requestId);

        var protocol = await LoadForMeetingAsync(requestId, датаЗаседания);
        var isNew = protocol is null;

        if (protocol is { Status: ProtocolStatus.Approved })
            throw new InvalidOperationException("Протокол утверждён — пересборка невозможна");

        protocol ??= new ProcurementProtocol
        {
            RequestId = requestId,
            ProtocolDate = _clock.Today,
            MeetingDate = датаЗаседания,
            MethodTitle = request.Method!.TitleRu,
            Subject = request.Subject,
            ContentHash = string.Empty,
        };

        protocol.MethodTitle = request.Method!.TitleRu;
        protocol.Subject = request.Subject;
        protocol.InitiatorUnitTitle = request.InitiatorUnit?.TitleRu;

        // Резервный поставщик — следующее по цене допущенное предложение: при отказе
        // победителя закупку не проводят заново, договор заключают с ним.
        var reserve = варианты
            .Where(v => v.Eligible && v.Key != winner.Key)
            .OrderBy(v => v.Price)
            .FirstOrDefault();

        protocol.MainSupplierId = winner.SupplierId;
        protocol.MainAmount = winner.Price;
        protocol.ReserveSupplierId = reserve?.SupplierId;
        protocol.ReserveAmount = reserve?.Price;

        // Строки пересобираются целиком: протокол отражает состояние отбора на момент
        // формирования, частичное обновление дало бы смешанную картину.
        if (!isNew)
            _db.ProtocolRows.RemoveRange(_db.ProtocolRows.Where(r => r.ProtocolId == protocol.Id));

        var order = 1;
        protocol.Rows = варианты
            .OrderBy(v => v.Price)
            .Select(v => new ProtocolRow
            {
                Order = order++,
                SupplierTitle = v.SupplierTitle,
                SupplierInn = v.SupplierInn,
                Price = v.Price,
                Specification = v.Specification,
                DeliveryTerms = v.DeliveryTerms,
                PaymentTerms = v.PaymentTerms,
                InitiatorConclusion = v.Conclusion,
                IsWinner = v.IsWinner,
            })
            .ToList();

        protocol.ContentHash = ComputeHash(protocol);

        if (isNew)
        {
            _db.ProcurementProtocols.Add(protocol);
            await _db.SaveChangesAsync();

            protocol.RegNumber = await NextNumberAsync();
            protocol.Status = ProtocolStatus.Draft;
        }
        else
        {
            // Пересборка меняет содержание — ранее поставленные подписи больше
            // не относятся к этому тексту (SIG-01).
            await RevokeSignaturesAsync(protocol.Id, "Протокол пересобран после изменения предложений");
            protocol.Status = ProtocolStatus.Draft;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementProtocol", protocol.Id, isNew ? "Generated" : "Regenerated", actorUserId, new
        {
            protocol.RegNumber,
            winner = winner.SupplierTitle,
            amount = winner.Price,
        });

        return await BuildAsync((await LoadAsync(requestId))!);
    }

    public async Task<ProtocolDto> UpdateAsync(int requestId, ProtocolUpdateRequest request, int actorUserId)
    {
        var protocol = await LoadAsync(requestId)
                       ?? throw new KeyNotFoundException("Протокол по этой закупке не сформирован");

        if (protocol.Status == ProtocolStatus.Approved)
            throw new InvalidOperationException("Протокол утверждён — правка разделов невозможна");

        protocol.BudgetNote = request.BudgetNote?.Trim();
        protocol.ExpertOpinion = request.ExpertOpinion?.Trim();
        protocol.DissentingOpinion = request.DissentingOpinion?.Trim();
        protocol.Recommendations = request.Recommendations?.Trim();
        protocol.SelectionBasis = request.SelectionBasis?.Trim();

        // Разделы входят в подписываемое содержание: после правки хеш меняется,
        // и уже стоящие подписи аннулируются.
        var newHash = ComputeHash(protocol);
        if (newHash != protocol.ContentHash)
        {
            protocol.ContentHash = newHash;
            await RevokeSignaturesAsync(protocol.Id, "Изменены разделы протокола");
            if (protocol.Status == ProtocolStatus.Signing)
                protocol.Status = ProtocolStatus.Draft;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementProtocol", protocol.Id, "Updated", actorUserId, null);

        return await BuildAsync((await LoadAsync(requestId))!);
    }

    public async Task<ProtocolDto> SignAsync(int requestId, ProtocolSignRequest request, int actorUserId)
    {
        var protocol = await LoadAsync(requestId)
                       ?? throw new KeyNotFoundException("Протокол по этой закупке не сформирован");

        var card = await BuildAsync(protocol);
        if (card.Blockers.Count > 0)
            throw new InvalidOperationException(string.Join("; ", card.Blockers));

        var already = protocol.Signatures.FirstOrDefault(s => s.Role == request.Role && !s.Revoked);
        if (already is not null)
            throw new InvalidOperationException($"{RoleTitle(request.Role)} уже подписал протокол");

        _db.ProtocolSignatures.Add(new ProtocolSignature
        {
            ProtocolId = protocol.Id,
            Role = request.Role,
            UserId = actorUserId,
            Level = request.Level == 0 ? SignatureLevel.Simple : SignatureLevel.Qualified,
            At = DateTime.UtcNow,
            ContentHash = protocol.ContentHash,
        });

        await _db.SaveChangesAsync();

        // Протокол утверждён, когда подписали все три стороны из подвала формы.
        var signed = await _db.ProtocolSignatures
            .Where(s => s.ProtocolId == protocol.Id && !s.Revoked)
            .Select(s => s.Role)
            .Distinct()
            .CountAsync();

        protocol.Status = signed >= 3 ? ProtocolStatus.Approved : ProtocolStatus.Signing;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementProtocol", protocol.Id, "Signed", actorUserId, new
        {
            role = RoleTitle(request.Role),
            level = request.Level == 0 ? "ПЭП" : "КЭП",
            protocol.ContentHash,
        });

        return await BuildAsync((await LoadAsync(requestId))!);
    }

    /// <summary>
    /// Последний протокол заявки. Протоколов теперь несколько — по одному на
    /// заседание комиссии, — и «протокол закупки» без уточнения означает
    /// последний: именно его подписывают и по нему заключают договор.
    /// </summary>
    private async Task<ProcurementProtocol?> LoadAsync(int requestId) =>
        await ProtocolQuery()
            .Where(p => p.RequestId == requestId)
            .OrderByDescending(p => p.MeetingDate ?? p.ProtocolDate)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync();

    /// <summary>Протокол конкретного заседания — по нему решается, создавать новый или пересобрать.</summary>
    private async Task<ProcurementProtocol?> LoadForMeetingAsync(int requestId, DateOnly? meetingDate) =>
        await ProtocolQuery()
            .Where(p => p.RequestId == requestId && p.MeetingDate == meetingDate)
            .FirstOrDefaultAsync();

    private IQueryable<ProcurementProtocol> ProtocolQuery() =>
        _db.ProcurementProtocols
            .Include(p => p.Rows)
            .Include(p => p.Signatures).ThenInclude(s => s.User)
            .Include(p => p.MainSupplier)
            .Include(p => p.ReserveSupplier);

    private async Task RevokeSignaturesAsync(int protocolId, string reason)
    {
        var signatures = await _db.ProtocolSignatures
            .Where(s => s.ProtocolId == protocolId && !s.Revoked)
            .ToListAsync();

        foreach (var s in signatures)
        {
            s.Revoked = true;
            s.RevokedReason = reason;
        }
    }

    private async Task<string> NextNumberAsync()
    {
        var year = _clock.Today.Year;
        var prefix = $"ПЗ-{year}-";

        var last = await _db.ProcurementProtocols
            .Where(p => p.RegNumber != null && p.RegNumber.StartsWith(prefix))
            .OrderByDescending(p => p.RegNumber)
            .Select(p => p.RegNumber)
            .FirstOrDefaultAsync();

        var seq = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{seq:D4}";
    }

    /// <summary>
    /// Варианты отбора по заявке — то, между чем выбирала комиссия.
    ///
    /// Источник зависит от способа закупки: у конкурса это конкурсные заявки, а
    /// коммерческих предложений там нет вовсе. Метод один на всех, кому нужен
    /// этот список: строки протокола и сверка его с текущим отбором обязаны
    /// смотреть в одно место, иначе протокол сравнивается с пустотой и вечно
    /// считается устаревшим.
    /// </summary>
    private async Task<List<ProtocolOption>> ВариантыОтбораАsync(int requestId)
    {
        var tender = await _db.Tenders
            .Include(t => t.Bids).ThenInclude(b => b.Supplier)
            .Include(t => t.Bids).ThenInclude(b => b.Votes).ThenInclude(v => v.Member).ThenInclude(m => m!.User)
            .Include(t => t.Commission).ThenInclude(m => m.User)
            .Where(t => t.RequestId == requestId
                        && t.Status != TenderStatus.Cancelled
                        && t.Status != TenderStatus.Failed)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        return tender is not null
            ? TenderProtocolRows.Build(tender)
            : TenderProtocolRows.Build(await _proposals.GetComparisonAsync(requestId));
    }

    private async Task<ProtocolDto> BuildAsync(ProcurementProtocol p)
    {
        // Протокол сверяется с текущим отбором: если состав вариантов или
        // победитель изменились после формирования, документ помечается устаревшим.
        var варианты = await ВариантыОтбораАsync(p.RequestId);
        var currentWinner = варианты.FirstOrDefault(x => x.IsWinner);
        var lowestPrice = варианты.Where(v => v.Eligible).Select(v => (decimal?)v.Price).Min();

        var dto = new ProtocolDto
        {
            Id = p.Id,
            RequestId = p.RequestId,
            RegNumber = p.RegNumber,
            ProtocolDate = p.ProtocolDate,
            MeetingDate = p.MeetingDate,
            Status = p.Status,
            StatusTitle = StatusTitle(p.Status),
            MethodTitle = p.MethodTitle,
            Subject = p.Subject,
            InitiatorUnitTitle = p.InitiatorUnitTitle,
            MainSupplierTitle = p.MainSupplier?.Title,
            MainAmount = p.MainAmount,
            ReserveSupplierTitle = p.ReserveSupplier?.Title,
            ReserveAmount = p.ReserveAmount,
            BudgetNote = p.BudgetNote,
            ExpertOpinion = p.ExpertOpinion,
            DissentingOpinion = p.DissentingOpinion,
            Recommendations = p.Recommendations,
            SelectionBasis = p.SelectionBasis,
            Rows = p.Rows.OrderBy(r => r.Order).Select(r => new ProtocolRowDto
            {
                Order = r.Order,
                SupplierTitle = r.SupplierTitle,
                SupplierInn = r.SupplierInn,
                Price = r.Price,
                Specification = r.Specification,
                DeliveryTerms = r.DeliveryTerms,
                PaymentTerms = r.PaymentTerms,
                InitiatorConclusion = r.InitiatorConclusion,
                IsWinner = r.IsWinner,
            }).ToList(),
            Signatures = p.Signatures.OrderBy(s => s.Role).Select(s => new ProtocolSignatureDto
            {
                Role = s.Role,
                RoleTitle = RoleTitle(s.Role),
                UserName = s.User?.FullName ?? "—",
                LevelTitle = s.Level == SignatureLevel.Qualified ? "КЭП" : "ПЭП",
                At = s.At,
                Revoked = s.Revoked,
                RevokedReason = s.RevokedReason,
            }).ToList(),
        };

        dto.IsOutdated =
            currentWinner is null ||
            currentWinner.SupplierId != p.MainSupplierId ||
            currentWinner.Price != p.MainAmount ||
            варианты.Count != p.Rows.Count;

        dto.RequiresSelectionBasis =
            lowestPrice is { } lowest && p.MainAmount is { } main && main > lowest;

        if (dto.RequiresSelectionBasis && string.IsNullOrWhiteSpace(p.SelectionBasis))
            dto.Blockers.Add("Победитель не с наименьшей ценой — заполните основание выбора (PRC-12)");

        if (string.IsNullOrWhiteSpace(p.BudgetNote))
            dto.Blockers.Add("Не заполнена виза УПиА о наличии средств в пределах бюджета");

        if (dto.IsOutdated)
            dto.Blockers.Add("Предложения изменились после формирования — пересоберите протокол");

        return dto;
    }

    /// <summary>
    /// Хеш подписываемого содержания: заголовок, решение и все строки таблицы.
    /// Ровно то, что напечатано в форме и под чем стоит подпись.
    /// </summary>
    private static string ComputeHash(ProcurementProtocol p)
    {
        var sb = new StringBuilder()
            .Append(p.Subject).Append('|')
            .Append(p.MethodTitle).Append('|')
            .Append(p.InitiatorUnitTitle).Append('|')
            .Append(p.MainSupplierId).Append('|').Append(p.MainAmount).Append('|')
            .Append(p.ReserveSupplierId).Append('|').Append(p.ReserveAmount).Append('|')
            .Append(p.BudgetNote).Append('|')
            .Append(p.ExpertOpinion).Append('|')
            .Append(p.DissentingOpinion).Append('|')
            .Append(p.Recommendations).Append('|')
            .Append(p.SelectionBasis).Append('|');

        foreach (var r in p.Rows.OrderBy(r => r.Order))
            sb.Append(r.Order).Append(':')
              .Append(r.SupplierTitle).Append(':')
              .Append(r.Price).Append(':')
              .Append(r.InitiatorConclusion).Append(':')
              .Append(r.IsWinner).Append(';');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Conclusion(ProposalDto p) => p switch
    {
        {SupplierBlacklisted: true} => "Не допущен: поставщик в чёрном списке",
        {MeetsRequirements: false} => $"Отклонено: {p.RejectionReason}",
        {MeetsRequirements: true} => "Соответствует техническим требованиям",
        _ => "Заключение не дано",
    };

    private static string StatusTitle(ProtocolStatus status) => status switch
    {
        ProtocolStatus.Signing => "На подписании",
        ProtocolStatus.Approved => "Утверждён",
        _ => "Черновик",
    };

    private static string RoleTitle(ProtocolSignerRole role) => role switch
    {
        ProtocolSignerRole.OrganizerCurator => "Куратор организатора закупки",
        ProtocolSignerRole.Approver => "Заместитель Председателя Правления",
        _ => "Инициатор",
    };
}
