using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IContractService
{
    Task<List<ContractDto>> ListAsync(int? requestId);
    Task<ContractDto> GetAsync(int id);
    Task<ContractDto> CreateAsync(int requestId, ContractCreateRequest request, int actorUserId);
    Task<ContractDto> UpdateAsync(int id, ContractUpdateRequest request, int actorUserId);
    Task<ContractDto> AddActAsync(int id, DeliveryActRequest request, int actorUserId);
    Task<ContractDto> ApproveActAsync(int actId, bool asCurator, int actorUserId);
    Task<ContractDto> TerminateAsync(int id, ContractTerminateRequest request, int actorUserId);

    /// <summary>
    /// Приобрести у поставщика дополнительное количество — в пределах четверти
    /// стоимости договора и по согласованной служебной записке.
    /// </summary>
    Task<ContractDto> TopUpAsync(int id, ContractTopUpRequest request, int actorUserId);
}

/// <summary>
/// Договор по итогам закупки и контроль исполнения (PRC-18/19).
///
/// Договор ложится на единую карточку документа: номер, статус и аудит живут там,
/// как у заявки и записки. Основание заключения (протокол или решение комиссии)
/// хранится ссылкой — без него непонятно, откуда взялось обязательство.
/// </summary>
public class ContractService : IContractService
{
    private const string NumberPattern = "ДЗ-{year}-{seq:D4}";

    /// <summary>Порог, свыше которого акт дополнительно утверждает куратор Правления (PRC-19).</summary>
    private const string CuratorActThresholdCode = "CuratorActApprovalThreshold";
    private const decimal DefaultCuratorActThreshold = 1_000_000m;

    /// <summary>
    /// Предел дополнительного количества — четверть стоимости договора,
    /// заключённого по результатам конкурса (п. 6 и 7 раздела VIII Положения).
    /// </summary>
    private const decimal TopUpShare = 0.25m;

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public ContractService(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<ContractDto>> ListAsync(int? requestId)
    {
        var q = Query();
        if (requestId is { } id)
            q = q.Where(c => c.RequestId == id);

        var rows = await q.OrderByDescending(c => c.Id).ToListAsync();
        var threshold = await CuratorThresholdAsync();
        return rows.Select(c => Build(c, threshold)).ToList();
    }

    public async Task<ContractDto> GetAsync(int id)
    {
        var contract = await LoadAsync(id);
        return Build(contract, await CuratorThresholdAsync());
    }

    public async Task<ContractDto> CreateAsync(int requestId, ContractCreateRequest request, int actorUserId)
    {
        var procurement = await _db.ProcurementRequests
            .Include(r => r.Method)
            .Include(r => r.Document)
            .Include(r => r.InitiatorUnit)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Заявка на закупку {requestId} не найдена");

        if (await _db.ProcurementContracts.AnyAsync(c => c.RequestId == requestId
                                                         && c.Status != ContractStatus.Terminated))
            throw new InvalidOperationException("По этой закупке уже заключён договор");

        var protocol = await _db.ProcurementProtocols
            .FirstOrDefaultAsync(p => p.RequestId == requestId);

        var tender = await _db.Tenders
            .Include(t => t.Bids)
            .Where(t => t.RequestId == requestId && t.Status == TenderStatus.Decided)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        // Победитель запроса ценовых предложений — коммерческое предложение, а не
        // конкурсная заявка. Этим способом идёт большинство закупок банка, и без
        // него договор по ним заключить было нельзя.
        var winningProposal = await _db.CommercialProposals
            .Where(p => p.RequestId == requestId && p.IsWinner)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        // Поставщик берётся из состоявшейся процедуры: договор заключается
        // с победителем, а не с произвольным контрагентом.
        var supplierId = request.SupplierId
                         ?? tender?.Bids.FirstOrDefault(b => b.IsWinner)?.SupplierId
                         ?? winningProposal?.SupplierId
                         ?? protocol?.MainSupplierId
                         ?? throw new InvalidOperationException(
                             "Победитель закупки не определён — договор заключать не с кем");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId)
                       ?? throw new KeyNotFoundException("Поставщик не найден");

        if (supplier.IsBlacklisted)
            throw new InvalidOperationException(
                $"Поставщик «{supplier.Title}» в чёрном списке — договор с ним не заключается");

        // Сумма договора — цена победившего предложения, а не ориентировочная сумма
        // заявки: заявку писали до того, как узнали цены.
        var amount = request.Amount
                     ?? tender?.Bids.FirstOrDefault(b => b.IsWinner)?.Price
                     ?? winningProposal?.Price
                     ?? protocol?.MainAmount
                     ?? procurement.Amount;

        var document = await _documents.CreateAsync(
            DocumentType.Contract, $"Договор по закупке «{procurement.Subject}»", actorUserId,
            request.SignedOn is null ? "Draft" : "Active");

        var contract = new ProcurementContract
        {
            DocumentId = document.Id,
            RequestId = requestId,
            ProtocolId = protocol?.Id,
            TenderId = tender?.Id,
            SupplierId = supplier.Id,
            Amount = amount,

            // Стоимость при заключении запоминается отдельно: от неё считается
            // право на дополнительное количество, и она не меняется допоставками.
            InitialAmount = amount,
            SignedOn = request.SignedOn,
            DeliveryDeadline = request.DeliveryDeadline,
            PaymentDeadline = request.PaymentDeadline,
            ResponsibleUserId = request.ResponsibleUserId ?? DefaultResponsible(procurement, tender),

            // Договор, заведённый сразу с датой подписания, уже исполняется:
            // иначе он навсегда остался бы проектом и акты не закрыли бы обязательство.
            Status = request.SignedOn is null ? ContractStatus.Draft : ContractStatus.Active,
        };

        _db.ProcurementContracts.Add(contract);
        await _db.SaveChangesAsync();

        await _documents.RegisterAsync(document.Id, "Contract", "global", NumberPattern, actorUserId);

        await _audit.LogAsync("ProcurementContract", contract.Id, "Created", actorUserId, new
        {
            requestId,
            supplier = supplier.Title,
            amount,
            basis = tender is not null ? $"конкурс {tender.RegNumber}" : $"протокол {protocol?.RegNumber}",
        });

        return await GetAsync(contract.Id);
    }

    public async Task<ContractDto> UpdateAsync(int id, ContractUpdateRequest request, int actorUserId)
    {
        var contract = await LoadAsync(id);

        if (contract.Status == ContractStatus.Terminated)
            throw new InvalidOperationException("Договор расторгнут — правка невозможна");

        contract.SignedOn = request.SignedOn ?? contract.SignedOn;
        contract.DeliveryDeadline = request.DeliveryDeadline ?? contract.DeliveryDeadline;
        contract.PaymentDeadline = request.PaymentDeadline ?? contract.PaymentDeadline;
        contract.ResponsibleUserId = request.ResponsibleUserId ?? contract.ResponsibleUserId;

        // Подписанный договор переходит в исполнение: с этого момента считаются
        // сроки поставки и оплаты.
        if (contract.SignedOn is not null && contract.Status == ContractStatus.Draft)
        {
            contract.Status = ContractStatus.Active;
            await _documents.ChangeStatusAsync(contract.DocumentId, "Active", actorUserId);
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementContract", contract.Id, "Updated", actorUserId, null);

        return await GetAsync(id);
    }

    public async Task<ContractDto> AddActAsync(int id, DeliveryActRequest request, int actorUserId)
    {
        var contract = await LoadAsync(id);

        if (contract.Status == ContractStatus.Terminated)
            throw new InvalidOperationException("Договор расторгнут — акты не регистрируются");

        if (string.IsNullOrWhiteSpace(request.Number))
            throw new ArgumentException("Укажите номер акта");

        if (request.Amount <= 0)
            throw new ArgumentException("Укажите сумму акта");

        var accepted = contract.Acts.Sum(a => a.Amount);
        if (accepted + request.Amount > contract.Amount)
            throw new InvalidOperationException(
                $"Сумма актов превысит договор: принято {accepted:N0} из {contract.Amount:N0} сом");

        _db.DeliveryActs.Add(new DeliveryAct
        {
            ContractId = id,
            Number = request.Number.Trim(),
            ActDate = request.ActDate ?? _clock.Today,
            Amount = request.Amount,
            Subject = request.Subject?.Trim(),
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementContract", id, "ActRegistered", actorUserId, new
        {
            request.Number,
            request.Amount,
        });

        return await GetAsync(id);
    }

    /// <summary>
    /// Сколько ещё можно докупить. У договора не по конкурсу права нет вовсе,
    /// поэтому ноль — это отсутствие права, а не исчерпанный предел.
    /// </summary>
    private static decimal ДоступнаяДопоставка(ProcurementContract c)
    {
        if (c.TenderId is null) return 0m;

        var основание = c.InitialAmount > 0 ? c.InitialAmount : c.Amount;
        var предел = Math.Round(основание * TopUpShare, 2, MidpointRounding.AwayFromZero);
        var использовано = c.Amount - основание;

        return Math.Max(0m, предел - использовано);
    }

    public async Task<ContractDto> TopUpAsync(int id, ContractTopUpRequest request, int actorUserId)
    {
        var contract = await Query().FirstOrDefaultAsync(c => c.Id == id)
                       ?? throw new KeyNotFoundException("Договор не найден");

        if (contract.Status == ContractStatus.Terminated)
            throw new InvalidOperationException("Договор расторгнут — дополнительное количество не приобретается");

        if (request.Amount <= 0)
            throw new ArgumentException("Укажите сумму дополнительного количества");

        // Право дано для договоров по итогам конкурса: у прямого заключения и
        // простой закупки такой оговорки в Положении нет.
        if (contract.TenderId is null)
            throw new InvalidOperationException(
                "Дополнительное количество приобретается по договору, заключённому по результатам конкурса (п. 6/7 раздела VIII)");

        if (request.SzId is null)
            throw new ArgumentException(
                "Укажите служебную записку, согласованную с куратором инициатора, организатором и куратором организатора");

        // Основание — стоимость при заключении, а не текущая: иначе каждая
        // допоставка расширяла бы право на следующую, и четверть превращалась бы
        // в бесконечный ряд.
        var основание = contract.InitialAmount > 0 ? contract.InitialAmount : contract.Amount;
        var предел = Math.Round(основание * TopUpShare, 2, MidpointRounding.AwayFromZero);
        var использовано = contract.Amount - основание;

        if (использовано + request.Amount > предел)
            throw new InvalidOperationException(
                $"Дополнительное количество не может превышать {TopUpShare * 100:0}% стоимости договора: " +
                $"предел {предел:N2} сом, уже приобретено {использовано:N2} сом");

        if (contract.InitialAmount <= 0) contract.InitialAmount = основание;

        contract.Amount += request.Amount;
        contract.TopUpSzId = request.SzId;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementContract", contract.Id, "ToppedUp", actorUserId, new
        {
            request.Amount,
            request.SzId,
            total = contract.Amount,
        });

        return (await GetAsync(contract.Id))!;
    }

    public async Task<ContractDto> ApproveActAsync(int actId, bool asCurator, int actorUserId)
    {
        var act = await _db.DeliveryActs.FirstOrDefaultAsync(a => a.Id == actId)
                  ?? throw new KeyNotFoundException("Акт не найден");

        var threshold = await CuratorThresholdAsync();

        await ПроверитьПравоНаПриёмкуАsync(act.ContractId, asCurator, actorUserId);

        if (asCurator)
        {
            if (act.Amount <= threshold)
                throw new InvalidOperationException(
                    $"Виза куратора Правления требуется при сумме свыше {threshold:N0} сом");

            // Куратор визирует после начальника СП: иначе теряется порядок,
            // в котором приёмка подтверждается по Положению.
            if (act.UnitHeadApprovedAt is null)
                throw new InvalidOperationException(
                    "Сначала акт утверждает начальник инициирующего подразделения");

            act.ApprovedByCuratorId = actorUserId;
            act.CuratorApprovedAt = DateTime.UtcNow;
        }
        else
        {
            act.ApprovedByUnitHeadId = actorUserId;
            act.UnitHeadApprovedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementContract", act.ContractId,
            asCurator ? "ActApprovedByCurator" : "ActApprovedByUnitHead", actorUserId, new
            {
                act.Number,
                act.Amount,
            });

        var contract = await LoadAsync(act.ContractId);
        var card = Build(contract, threshold);

        // Договор закрывается, когда принятая сумма покрывает обязательство
        // и все акты собрали требуемые утверждения.
        if (contract.Status == ContractStatus.Active
            && card.AcceptedAmount >= contract.Amount
            && card.Acts.All(a => a.IsApproved))
        {
            contract.Status = ContractStatus.Completed;
            await _documents.ChangeStatusAsync(contract.DocumentId, "Completed", actorUserId);

            // Закрывается не только договор, но и закупка: обязательство исполнено,
            // и заявка, с которой всё началось, больше не «в закупке». Без этого она
            // оставалась в работе навсегда, а счётчик завершённых стоял на нуле.
            var заявка = await _db.ProcurementRequests
                .Include(r => r.Document)
                .FirstOrDefaultAsync(r => r.Id == contract.RequestId);

            if (заявка?.Document is {StatusCode: not ProcurementStatus.Completed})
            {
                await _documents.ChangeStatusAsync(
                    заявка.DocumentId, ProcurementStatus.Completed, actorUserId);

                await _audit.LogAsync("ProcurementRequest", заявка.Id, "Completed", actorUserId,
                    new {contract = contract.Id});
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync("ProcurementContract", contract.Id, "Completed", actorUserId, null);
        }

        return await GetAsync(act.ContractId);
    }

    public async Task<ContractDto> TerminateAsync(int id, ContractTerminateRequest request, int actorUserId)
    {
        var contract = await LoadAsync(id);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Укажите основание расторжения");

        if (contract.Status == ContractStatus.Completed)
            throw new InvalidOperationException("Договор исполнен — расторжение невозможно");

        contract.Status = ContractStatus.Terminated;
        contract.TerminationReason = request.Reason.Trim();

        await _documents.ChangeStatusAsync(contract.DocumentId, "Terminated", actorUserId);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ProcurementContract", id, "Terminated", actorUserId, new {request.Reason});

        return await GetAsync(id);
    }

    private IQueryable<ProcurementContract> Query() =>
        _db.ProcurementContracts
            .Include(c => c.Document)
            .Include(c => c.Request).ThenInclude(r => r!.Document)
            .Include(c => c.Request).ThenInclude(r => r!.Method)
            .Include(c => c.Protocol)
            .Include(c => c.Tender)
            .Include(c => c.Supplier)
            .Include(c => c.ResponsibleUser)
            .Include(c => c.Acts).ThenInclude(a => a.ApprovedByUnitHead)
            .Include(c => c.Acts).ThenInclude(a => a.ApprovedByCurator);

    private async Task<ProcurementContract> LoadAsync(int id) =>
        await Query().FirstOrDefaultAsync(c => c.Id == id)
        ?? throw new KeyNotFoundException($"Договор {id} не найден");

    /// <summary>
    /// Кто подтверждает приёмку.
    ///
    /// Акт утверждает начальник инициирующего подразделения, а свыше порога — ещё
    /// и курирующий член Правления. Раньше действие было закрыто правом ведения
    /// договоров, то есть правом Сектора закупок: приёмку подтверждал не тот, кто
    /// принимал, а тот, кто закупал. Ради этого разделения визы и заведены.
    /// </summary>
    private async Task ПроверитьПравоНаПриёмкуАsync(int contractId, bool asCurator, int actorUserId)
    {
        var роли = await _db.ProcurementContracts
            .Where(c => c.Id == contractId)
            .Select(c => new
            {
                Head = c.Request!.InitiatorUnit!.HeadUserId,
                UnitCurator = c.Request.InitiatorUnit.CuratorUserId,
                RequestCurator = c.Request.CuratorUserId,
            })
            .FirstOrDefaultAsync();

        if (роли is null) return;

        if (asCurator)
        {
            // Куратор закупки записан в самой заявке решением матрицы; если его
            // там нет, действует куратор подразделения.
            var куратор = роли.RequestCurator ?? роли.UnitCurator;

            if (куратор is not null && куратор != actorUserId)
                throw new UnauthorizedAccessException(
                    "Визу ставит курирующий член Правления, закреплённый за этой закупкой");

            return;
        }

        if (роли.Head is not null && роли.Head != actorUserId)
            throw new UnauthorizedAccessException(
                "Акт утверждает начальник инициирующего подразделения");
    }

    private async Task<decimal> CuratorThresholdAsync()
    {
        var value = await _db.ProcurementParameters
            .Where(p => p.Code == CuratorActThresholdCode)
            .Select(p => (decimal?)p.Value)
            .FirstOrDefaultAsync();

        return value ?? DefaultCuratorActThreshold;
    }

    /// <summary>
    /// Кто готовит договор (PRC-18): товар — Сектор закупок, товар с установкой
    /// и итоги конкурса — инициатор. Возвращается автор заявки либо остаётся пустым,
    /// если ответственного назначат вручную.
    /// </summary>
    private static int? DefaultResponsible(ProcurementRequest request, Tender? tender) =>
        tender is not null || request.SubjectKind == ProcurementSubjectKind.GoodsWithInstallation
            ? request.Document?.AuthorId
            : null;

    private ContractDto Build(ProcurementContract c, decimal curatorThreshold)
    {
        var acts = c.Acts.OrderBy(a => a.ActDate).Select(a =>
        {
            var requiresCurator = a.Amount > curatorThreshold;
            return new DeliveryActDto
            {
                Id = a.Id,
                Number = a.Number,
                ActDate = a.ActDate,
                Amount = a.Amount,
                Subject = a.Subject,
                ApprovedByUnitHead = a.ApprovedByUnitHead?.FullName,
                UnitHeadApprovedAt = a.UnitHeadApprovedAt,
                ApprovedByCurator = a.ApprovedByCurator?.FullName,
                CuratorApprovedAt = a.CuratorApprovedAt,
                RequiresCuratorApproval = requiresCurator,
                IsApproved = a.UnitHeadApprovedAt is not null
                             && (!requiresCurator || a.CuratorApprovedAt is not null),
            };
        }).ToList();

        var dto = new ContractDto
        {
            Id = c.Id,
            DocumentId = c.DocumentId,
            RegNumber = c.Document?.RegNumber,
            RequestId = c.RequestId,
            RequestRegNumber = c.Request?.Document?.RegNumber,
            Subject = c.Request?.Subject ?? "—",
            ProtocolId = c.ProtocolId,
            ProtocolRegNumber = c.Protocol?.RegNumber,
            TenderId = c.TenderId,
            TenderRegNumber = c.Tender?.RegNumber,
            SupplierTitle = c.Supplier?.Title ?? "—",
            SupplierInn = c.Supplier?.Inn,
            Status = c.Status,
            StatusTitle = StatusTitle(c.Status),
            Amount = c.Amount,
            InitialAmount = c.InitialAmount > 0 ? c.InitialAmount : c.Amount,
            TopUpAvailable = ДоступнаяДопоставка(c),
            SignedOn = c.SignedOn,
            DeliveryDeadline = c.DeliveryDeadline,
            PaymentDeadline = c.PaymentDeadline,
            ResponsibleName = c.ResponsibleUser?.FullName,
            ResponsibleRule = ResponsibleRule(c),
            TerminationReason = c.TerminationReason,
            Acts = acts,
            AcceptedAmount = acts.Where(a => a.IsApproved).Sum(a => a.Amount),
        };

        dto.IsDeliveryOverdue = c.Status == ContractStatus.Active
                                && c.DeliveryDeadline is { } deadline
                                && deadline < _clock.Today
                                && dto.AcceptedAmount < c.Amount;

        if (c.SignedOn is null)
            dto.Blockers.Add("Не указана дата подписания договора");

        if (c.DeliveryDeadline is null)
            dto.Blockers.Add("Не задан срок поставки — контроль исполнения не работает (PRC-19)");

        if (dto.IsDeliveryOverdue)
            dto.Blockers.Add(
                $"Срок поставки истёк {c.DeliveryDeadline:dd.MM.yyyy}, принято {dto.AcceptedAmount:N0} из {c.Amount:N0} сом");

        foreach (var act in acts.Where(a => !a.IsApproved))
            dto.Blockers.Add(act.UnitHeadApprovedAt is null
                ? $"Акт № {act.Number} не утверждён начальником СП"
                : $"Акт № {act.Number} ждёт визы курирующего члена Правления");

        return dto;
    }

    private static string ResponsibleRule(ProcurementContract c) =>
        c.TenderId is not null
            ? "По итогам конкурса договор готовит и заключает инициатор (PRC-18)"
            : c.Request?.SubjectKind == ProcurementSubjectKind.GoodsWithInstallation
                ? "Товар требует установки и ввода в эксплуатацию — договор готовит инициатор (PRC-18)"
                : "Закупка товара — договор готовит и заключает Сектор закупок (PRC-18)";

    private static string StatusTitle(ContractStatus status) => status switch
    {
        ContractStatus.Active => "Исполняется",
        ContractStatus.Completed => "Исполнен",
        ContractStatus.Terminated => "Расторгнут",
        _ => "Проект",
    };
}
