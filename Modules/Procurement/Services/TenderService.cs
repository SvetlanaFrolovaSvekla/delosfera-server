using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface ITenderService
{
    Task<TenderDto?> GetAsync(int requestId);
    Task<TenderDto> CreateAsync(int requestId, TenderCreateRequest request, int actorUserId);
    Task<TenderDto> AddMemberAsync(int tenderId, CommissionMemberRequest request, int actorUserId);
    Task<TenderDto> RemoveMemberAsync(int memberId, int actorUserId);
    Task<TenderDto> PublishAsync(int tenderId, TenderPublishRequest request, int actorUserId);
    Task<TenderDto> AddBidAsync(int tenderId, TenderBidRequest request, int actorUserId);
    Task<TenderDto> OpenBidsAsync(int tenderId, int actorUserId);
    Task<TenderDto> SetAttendanceAsync(int memberId, AttendanceRequest request, int actorUserId);
    Task<TenderDto> SetConclusionAsync(int memberId, ExpertConclusionRequest request, int actorUserId);
    Task<TenderDto> ScoreBidAsync(int bidId, BidScoreRequest request, int actorUserId);

    /// <summary>Назначить заседание комиссии или перенести его на другую дату.</summary>
    Task<TenderDto> ScheduleMeetingAsync(int tenderId, MeetingScheduleRequest request, int actorUserId);

    /// <summary>Внести результаты очного голосования по заявке (п. 24.2).</summary>
    Task<TenderDto> RecordVotesAsync(int bidId, BidVotesRequest request, int actorUserId);

    Task<TenderDto> DeclareWinnerAsync(int tenderId, int bidId, int actorUserId);
    Task<TenderDto> FailAsync(int tenderId, TenderFailRequest request, int actorUserId);
}

/// <summary>
/// Конкурс по закупке (PRC-13..17): комиссия, публикация, конкурсный период,
/// вскрытие заявок, оценка и определение победителя.
///
/// Состав комиссии задан п. 120 Положения жёстко и без порогов по сумме: пять
/// сотрудников Банка; председатель — член Правления, не курирующий инициатора
/// закупки; постоянные члены — УБУиО, Юридическая служба и Управление безопасности.
/// Раньше присутствие УБУиО и председателя-правленца включалось только выше сумм
/// 5 и 3 млн — таких порогов в Положении нет, и мелкие конкурсы проходили составом,
/// который оно не допускает.
/// </summary>
public class TenderService : ITenderService
{
    /// <summary>Минимальный конкурсный период в рабочих днях (PRC-13).</summary>
    private const int MinTenderPeriodWorkdays = 5;

    /// <summary>Стандартный состав комиссии по Положению.</summary>
    private const int DefaultCommissionSize = 5;

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IBankClock _clock;

    public TenderService(DelosferaDbContext db, IAuditService audit, IBankClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<TenderDto?> GetAsync(int requestId)
    {
        var tender = await LoadByRequestAsync(requestId);
        return tender is null ? null : await BuildAsync(tender);
    }

    public async Task<TenderDto> CreateAsync(int requestId, TenderCreateRequest request, int actorUserId)
    {
        var procurement = await _db.ProcurementRequests
            .Include(r => r.Method)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Заявка на закупку {requestId} не найдена");

        if (procurement.Method!.Code is not (ProcurementMethodCode.TenderOpen or ProcurementMethodCode.TenderLimited))
            throw new InvalidOperationException(
                $"Способ «{procurement.Method.TitleRu}» не предполагает конкурс");

        if (await _db.Tenders.AnyAsync(t => t.RequestId == requestId
                                            && t.Status != TenderStatus.Failed
                                            && t.Status != TenderStatus.Cancelled))
            throw new InvalidOperationException("По этой закупке уже идёт конкурс");

        var tender = new Tender
        {
            RequestId = requestId,
            IsLimited = request.IsLimited || procurement.Method.Code == ProcurementMethodCode.TenderLimited,
            SubmissionDeadline = request.SubmissionDeadline,
            CommissionOrderNumber = request.CommissionOrderNumber?.Trim(),
            CommissionOrderDate = request.CommissionOrderDate,
            PreviousTenderId = request.PreviousTenderId,
            RegNumber = await NextNumberAsync(),
        };

        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();

        // Повторный конкурс проводится тем же составом (PRC-16): копируем комиссию,
        // чтобы её не собирали заново и не меняли по ходу.
        if (request.PreviousTenderId is { } previousId)
        {
            var previous = await _db.CommissionMembers
                .Where(m => m.TenderId == previousId)
                .ToListAsync();

            foreach (var m in previous)
            {
                _db.CommissionMembers.Add(new CommissionMember
                {
                    TenderId = tender.Id,
                    UserId = m.UserId,
                    Role = m.Role,
                    IsBoardMember = m.IsBoardMember,
                    IsAccountant = m.IsAccountant,
                    IsLegal = m.IsLegal,
                    IsSecurity = m.IsSecurity,
                });
            }

            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync("Tender", tender.Id, "Created", actorUserId, new
        {
            tender.RegNumber,
            requestId,
            repeatOf = request.PreviousTenderId,
        });

        return await BuildAsync((await LoadAsync(tender.Id))!);
    }

    public async Task<TenderDto> AddMemberAsync(int tenderId, CommissionMemberRequest request, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (tender.Status >= TenderStatus.Opened)
            throw new InvalidOperationException("Состав комиссии не меняется после вскрытия заявок");

        if (!await _db.Users.AnyAsync(u => u.Id == request.UserId))
            throw new KeyNotFoundException($"Пользователь {request.UserId} не найден");

        if (tender.Commission.Any(m => m.UserId == request.UserId))
            throw new InvalidOperationException("Этот сотрудник уже в составе комиссии");

        if (request.Role == CommissionRole.Chairman && tender.Commission.Any(m => m.Role == CommissionRole.Chairman))
            throw new InvalidOperationException("Председатель комиссии уже назначен");

        // Секретарь у комиссии один: он ведёт протокол, и второй такой же означал бы
        // два протокола одного заседания.
        if (request.Role == CommissionRole.Secretary && tender.Commission.Any(m => m.Role == CommissionRole.Secretary))
            throw new InvalidOperationException("Секретарь комиссии уже назначен");

        _db.CommissionMembers.Add(new CommissionMember
        {
            TenderId = tenderId,
            UserId = request.UserId,
            Role = request.Role,
            IsBoardMember = request.IsBoardMember,
            IsAccountant = request.IsAccountant,
            IsLegal = request.IsLegal,
            IsSecurity = request.IsSecurity,
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, "CommissionMemberAdded", actorUserId, new {request.UserId, request.Role});

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    public async Task<TenderDto> RemoveMemberAsync(int memberId, int actorUserId)
    {
        var member = await _db.CommissionMembers.FirstOrDefaultAsync(m => m.Id == memberId)
                     ?? throw new KeyNotFoundException("Член комиссии не найден");

        var tender = await LoadAsync(member.TenderId)!;

        if (tender!.Status >= TenderStatus.Opened)
            throw new InvalidOperationException("Состав комиссии не меняется после вскрытия заявок");

        _db.CommissionMembers.Remove(member);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", member.TenderId, "CommissionMemberRemoved", actorUserId, new {member.UserId});

        return await BuildAsync((await LoadAsync(member.TenderId))!);
    }

    public async Task<TenderDto> PublishAsync(int tenderId, TenderPublishRequest request, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");
        var card = await BuildAsync(tender);

        if (tender.Status != TenderStatus.Draft)
            throw new InvalidOperationException("Объявляется только конкурс в подготовке");

        // Комиссия формируется приказом до объявления: после публикации менять
        // состав нельзя, значит проверять его надо здесь.
        var composition = card.Blockers.Where(b => b.StartsWith("Комиссия") || b.StartsWith("Не назначен")).ToList();
        if (composition.Count > 0)
            throw new InvalidOperationException(string.Join("; ", composition));

        var today = _clock.Today;
        var minDeadline = AddWorkdays(today, MinTenderPeriodWorkdays);

        if (request.SubmissionDeadline < minDeadline)
            throw new InvalidOperationException(
                $"Конкурсный период — не менее {MinTenderPeriodWorkdays} рабочих дней: срок приёма не раньше {minDeadline:dd.MM.yyyy} (PRC-13)");

        tender.PublishedOn = today;
        tender.SubmissionDeadline = request.SubmissionDeadline;
        tender.Status = TenderStatus.Published;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, "Published", actorUserId, new
        {
            tender.PublishedOn,
            tender.SubmissionDeadline,
            channel = tender.IsLimited ? "приглашения участникам" : "сайт Банка и tenders.kg",
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    public async Task<TenderDto> AddBidAsync(int tenderId, TenderBidRequest request, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (tender.Status is not (TenderStatus.Published or TenderStatus.Draft))
            throw new InvalidOperationException("Заявки принимаются до вскрытия");

        if (request.Price <= 0)
            throw new ArgumentException("Укажите цену конкурсной заявки");

        var supplier = await ResolveSupplierAsync(request);

        if (tender.Bids.Any(x => x.SupplierId == supplier.Id))
            throw new InvalidOperationException($"Заявка поставщика «{supplier.Title}» уже зарегистрирована");

        var submittedOn = request.SubmittedOn ?? _clock.Today;

        // Опоздавшая заявка регистрируется, но к вскрытию не допускается (PRC-15):
        // факт её поступления должен остаться в журнале.
        var isLate = tender.SubmissionDeadline is { } deadline && submittedOn > deadline;

        _db.TenderBids.Add(new TenderBid
        {
            TenderId = tenderId,
            SupplierId = supplier.Id,
            Price = request.Price,
            SubmittedOn = submittedOn,
            IsLate = isLate,
            Specification = request.Specification?.Trim(),
            RejectionReason = isLate ? "Заявка подана после окончательного срока приёма" : null,
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, "BidRegistered", actorUserId, new
        {
            supplier = supplier.Title,
            request.Price,
            isLate,
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    public async Task<TenderDto> OpenBidsAsync(int tenderId, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (tender.Status != TenderStatus.Published)
            throw new InvalidOperationException("Вскрываются заявки объявленного конкурса");

        if (tender.SubmissionDeadline is { } deadline && _clock.Today <= deadline)
            throw new InvalidOperationException(
                $"Срок приёма заявок истекает {deadline:dd.MM.yyyy} — вскрытие раньше срока не проводится");

        // Допуск: не опоздала и поставщик не в чёрном списке (PRC-17).
        foreach (var bid in tender.Bids)
        {
            if (bid.IsLate)
            {
                bid.IsAdmitted = false;
                continue;
            }

            if (bid.Supplier!.IsBlacklisted)
            {
                bid.IsAdmitted = false;
                bid.RejectionReason = "Поставщик в чёрном списке недобросовестных поставщиков";
                continue;
            }

            bid.IsAdmitted = true;
        }

        tender.OpenedOn = _clock.Today;
        tender.Status = TenderStatus.Opened;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, "BidsOpened", actorUserId, new
        {
            total = tender.Bids.Count,
            admitted = tender.Bids.Count(b => b.IsAdmitted),
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    public async Task<TenderDto> SetAttendanceAsync(int memberId, AttendanceRequest request, int actorUserId)
    {
        var member = await _db.CommissionMembers.FirstOrDefaultAsync(m => m.Id == memberId)
                     ?? throw new KeyNotFoundException("Член комиссии не найден");

        // Особое мнение — форма несогласия с решением, а решение принимают голосующие.
        // У эксперта и секретаря голоса нет, их позиция излагается иначе: заключением
        // и протоколом соответственно.
        if (!string.IsNullOrWhiteSpace(request.DissentingOpinion) && !IsVoting(member.Role))
            throw new InvalidOperationException(
                $"{RoleTitle(member.Role)} не голосует, поэтому особого мнения по решению не заявляет");

        member.AttendedOpening = request.Attended;
        member.DissentingOpinion = request.DissentingOpinion?.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", member.TenderId, "AttendanceMarked", actorUserId, new
        {
            member.UserId,
            request.Attended,
        });

        return await BuildAsync((await LoadAsync(member.TenderId))!);
    }

    public async Task<TenderDto> SetConclusionAsync(int memberId, ExpertConclusionRequest request, int actorUserId)
    {
        var member = await _db.CommissionMembers.FirstOrDefaultAsync(m => m.Id == memberId)
                     ?? throw new KeyNotFoundException("Член комиссии не найден");

        if (member.Role != CommissionRole.Expert)
            throw new InvalidOperationException("Заключение по предмету закупки даёт эксперт комиссии");

        if (string.IsNullOrWhiteSpace(request.Conclusion) && request.AttachmentId is null)
            throw new ArgumentException("Приложите заключение текстом или файлом");

        if (request.AttachmentId is { } attachmentId)
        {
            // Файл заключения должен лежать в этой же закупке: чужое вложение в
            // протоколе выглядело бы как подмена документа.
            var tender = await LoadAsync(member.TenderId) ?? throw new KeyNotFoundException("Конкурс не найден");
            var documentId = await _db.ProcurementRequests
                .Where(r => r.Id == tender.RequestId)
                .Select(r => r.DocumentId)
                .FirstAsync();

            var belongs = await _db.DocumentAttachments
                .AnyAsync(a => a.Id == attachmentId && a.DocumentId == documentId);

            if (!belongs)
                throw new InvalidOperationException("Файл заключения не найден среди вложений этой закупки");
        }

        member.Conclusion = request.Conclusion?.Trim();
        member.ConclusionAttachmentId = request.AttachmentId;
        member.ConclusionAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", member.TenderId, "ExpertConclusion", actorUserId, new
        {
            member.UserId,
            hasFile = request.AttachmentId is not null,
        });

        return await BuildAsync((await LoadAsync(member.TenderId))!);
    }

    /// <summary>Голосуют председатель и члены комиссии; секретарь и эксперт — нет.</summary>
    private static bool IsVoting(CommissionRole role) =>
        role is CommissionRole.Chairman or CommissionRole.Member;

    public async Task<TenderDto> ScoreBidAsync(int bidId, BidScoreRequest request, int actorUserId)
    {
        var bid = await _db.TenderBids.Include(b => b.Supplier).FirstOrDefaultAsync(b => b.Id == bidId)
                  ?? throw new KeyNotFoundException("Конкурсная заявка не найдена");

        if (!bid.IsAdmitted)
            throw new InvalidOperationException("Оценивается только допущенная заявка");

        bid.Score = request.Score;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", bid.TenderId, "BidScored", actorUserId, new
        {
            supplier = bid.Supplier?.Title,
            request.Score,
        });

        return await BuildAsync((await LoadAsync(bid.TenderId))!);
    }

    /// <summary>
    /// Назначить заседание или перенести его.
    ///
    /// Заседание срывается по обычным причинам — не собрался кворум, заболел
    /// председатель. У Сектора закупок на этом шаге два хода: сдвинуть дату или
    /// внести голоса состоявшегося заседания. Перенос требует основания: по
    /// срокам закупки потом задают вопросы.
    /// </summary>
    public async Task<TenderDto> ScheduleMeetingAsync(
        int tenderId, MeetingScheduleRequest request, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (tender.Status is TenderStatus.Decided or TenderStatus.Failed or TenderStatus.Cancelled)
            throw new InvalidOperationException("Конкурс завершён — заседание уже не назначить");

        var isMove = tender.MeetingDate is not null && tender.MeetingDate != request.Date;

        if (isMove && string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException(
                "Укажите основание переноса: оно печатается в протоколе и объясняет сдвиг сроков");

        _db.TenderMeetingChanges.Add(new TenderMeetingChange
        {
            TenderId = tender.Id,
            FromDate = tender.MeetingDate,
            ToDate = request.Date,
            Reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Заседание назначено"
                : request.Reason.Trim(),
            ByUserId = actorUserId,
            At = DateTime.UtcNow,
        });

        tender.MeetingDate = request.Date;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, isMove ? "MeetingMoved" : "MeetingScheduled", actorUserId, new
        {
            to = request.Date,
            request.Reason,
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    /// <summary>
    /// Внести результаты очного голосования по заявке (п. 24.2 Положения).
    ///
    /// Голоса вносит секретарь или Сектор закупок по итогам заседания: голосование
    /// очное, а система лишь фиксирует его результат. Повторное внесение заменяет
    /// голоса целиком — заседание переголосовало, а не добавило ещё по голосу.
    /// </summary>
    public async Task<TenderDto> RecordVotesAsync(int bidId, BidVotesRequest request, int actorUserId)
    {
        var bid = await _db.TenderBids
                      .Include(b => b.Supplier)
                      .Include(b => b.Votes)
                      .FirstOrDefaultAsync(b => b.Id == bidId)
                  ?? throw new KeyNotFoundException("Конкурсная заявка не найдена");

        if (!bid.IsAdmitted)
            throw new InvalidOperationException(
                "Голосуют только по допущенной заявке: недопущенная в отборе не участвует");

        var tender = await LoadAsync(bid.TenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (tender.Status != TenderStatus.Opened)
            throw new InvalidOperationException("Голосование проводится после вскрытия заявок");

        if (tender.MeetingDate is null)
            throw new InvalidOperationException(
                "Сначала назначьте дату заседания: решения принимаются очно (п. 24.2 Положения)");

        var voters = tender.Commission.Where(m => IsVoting(m.Role)).ToDictionary(m => m.Id);

        foreach (var vote in request.Votes)
        {
            if (!voters.TryGetValue(vote.MemberId, out var member))
                throw new InvalidOperationException(
                    "Голос принят только от члена комиссии с правом голоса: у секретаря и эксперта его нет");

            // Не голосовавший не может голосовать: явка и голос — разные вещи,
            // и расхождение между ними должно быть видно, а не сглажено.
            if (!member.AttendedOpening)
                throw new InvalidOperationException(
                    $"{member.User?.FullName ?? "Член комиссии"} не отмечен как участник заседания — голос принять нельзя");
        }

        _db.CommissionVotes.RemoveRange(bid.Votes);

        var now = DateTime.UtcNow;
        foreach (var vote in request.Votes)
        {
            _db.CommissionVotes.Add(new CommissionVote
            {
                BidId = bid.Id,
                MemberId = vote.MemberId,
                Choice = vote.Choice,
                RecordedByUserId = actorUserId,
                At = now,
            });
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", bid.TenderId, "VotesRecorded", actorUserId, new
        {
            supplier = bid.Supplier?.Title,
            meeting = tender.MeetingDate,
            forVotes = request.Votes.Count(v => v.Choice == VoteChoice.For),
            against = request.Votes.Count(v => v.Choice == VoteChoice.Against),
            abstained = request.Votes.Count(v => v.Choice == VoteChoice.Abstained),
        });

        return await BuildAsync((await LoadAsync(bid.TenderId))!);
    }

    public async Task<TenderDto> DeclareWinnerAsync(int tenderId, int bidId, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");
        var card = await BuildAsync(tender);

        if (tender.Status != TenderStatus.Opened)
            throw new InvalidOperationException("Победитель определяется после вскрытия заявок");

        if (!card.HasQuorum)
            throw new InvalidOperationException(
                $"Нет кворума: присутствует {card.Attended} из {card.QuorumRequired} требуемых членов комиссии (п. 24.1 Положения)");

        var winner = tender.Bids.FirstOrDefault(b => b.Id == bidId)
                     ?? throw new KeyNotFoundException("Заявка не найдена в этом конкурсе");

        if (!winner.IsAdmitted)
            throw new InvalidOperationException("Победителем может стать только допущенная заявка");

        // Победителя определяет голосование, а не нажатие кнопки: решение комиссии
        // принято, если за него большинство голосовавших (п. 24.2).
        var voted = card.Bids.FirstOrDefault(b => b.Id == bidId);

        if (voted is null || voted.Votes.Count == 0)
            throw new InvalidOperationException(
                "По заявке не внесены голоса комиссии: решение принимается открытым голосованием (п. 24.2 Положения)");

        if (!voted.Carried)
            throw new InvalidOperationException(
                voted.VoteOutcomeNote ?? "Заявка не набрала большинства голосов комиссии");

        foreach (var b in tender.Bids)
            b.IsWinner = b.Id == winner.Id;

        tender.Status = TenderStatus.Decided;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, "WinnerDeclared", actorUserId, new
        {
            supplier = winner.Supplier?.Title,
            winner.Price,
            attended = card.Attended,
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    public async Task<TenderDto> FailAsync(int tenderId, TenderFailRequest request, int actorUserId)
    {
        var tender = await LoadAsync(tenderId) ?? throw new KeyNotFoundException("Конкурс не найден");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Укажите основание: оно фиксируется в протоколе и уведомлении участникам");

        if (tender.Status is TenderStatus.Decided)
            throw new InvalidOperationException("Конкурс уже завершён решением комиссии");

        tender.Status = request.Cancel ? TenderStatus.Cancelled : TenderStatus.Failed;
        tender.FailureReason = request.Reason.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Tender", tenderId, request.Cancel ? "Cancelled" : "Failed", actorUserId, new
        {
            request.Reason,
        });

        return await BuildAsync((await LoadAsync(tenderId))!);
    }

    private IQueryable<Tender> Query() =>
        _db.Tenders
            .Include(t => t.Request).ThenInclude(r => r!.Method)
            .Include(t => t.Commission).ThenInclude(m => m.User)
            .Include(t => t.Bids).ThenInclude(b => b.Supplier)
            .Include(t => t.Bids).ThenInclude(b => b.Votes).ThenInclude(v => v.Member).ThenInclude(m => m!.User)
            .Include(t => t.MeetingChanges).ThenInclude(c => c.By);

    private async Task<Tender?> LoadAsync(int id) => await Query().FirstOrDefaultAsync(t => t.Id == id);

    private async Task<Tender?> LoadByRequestAsync(int requestId) =>
        await Query()
            .Where(t => t.RequestId == requestId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

    private async Task<Supplier> ResolveSupplierAsync(TenderBidRequest request)
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

    private async Task<string> NextNumberAsync()
    {
        var year = _clock.Today.Year;
        var prefix = $"КНК-{year}-";

        var last = await _db.Tenders
            .Where(t => t.RegNumber != null && t.RegNumber.StartsWith(prefix))
            .OrderByDescending(t => t.RegNumber)
            .Select(t => t.RegNumber)
            .FirstOrDefaultAsync();

        var seq = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{seq:D4}";
    }

    /// <summary>Прибавить рабочие дни: выходные в конкурсный период не входят.</summary>
    private static DateOnly AddWorkdays(DateOnly from, int workdays)
    {
        var date = from;
        var added = 0;

        while (added < workdays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                added++;
        }

        return date;
    }

    private async Task<TenderDto> BuildAsync(Tender t)
    {
        var parameters = await _db.ProcurementParameters.ToDictionaryAsync(x => x.Code, x => x.Value);

        decimal Param(string code, decimal fallback) =>
            parameters.TryGetValue(code, out var v) ? v : fallback;

        var amount = t.Request!.Amount;

        // Требования к составу берутся из правила матрицы, по которому построена заявка.
        var rule = t.Request.MatrixRuleId is { } ruleId
            ? await _db.AuthorityMatrixRules.FirstOrDefaultAsync(r => r.Id == ruleId)
            : null;

        // Куратор инициирующего подразделения: председателем комиссии он быть не
        // может (п. 120). Проверять это должна система, а не память секретаря —
        // иначе конфликт интересов вскроется на обжаловании, а не на заседании.
        var initiatorCuratorId = t.Request.InitiatorUnitId is { } unitId
            ? await _db.OrganizationUnits
                .Where(u => u.Id == unitId)
                .Select(u => u.CuratorUserId)
                .FirstOrDefaultAsync()
            : null;

        var dto = new TenderDto
        {
            Id = t.Id,
            RequestId = t.RequestId,
            RegNumber = t.RegNumber,
            Status = t.Status,
            StatusTitle = StatusTitle(t.Status),
            Subject = t.Request.Subject,
            Amount = amount,
            IsLimited = t.IsLimited,
            PublishedOn = t.PublishedOn,
            SubmissionDeadline = t.SubmissionDeadline,
            OpenedOn = t.OpenedOn,
            CommissionOrderNumber = t.CommissionOrderNumber,
            CommissionOrderDate = t.CommissionOrderDate,
            PreviousTenderId = t.PreviousTenderId,
            FailureReason = t.FailureReason,
            MeetingDate = t.MeetingDate,
            RequiredSize = rule?.CommissionSize ?? DefaultCommissionSize,
            RequiredBoardMembers = rule?.CommissionMinBoardMembers ?? 0,
            InitiatorCuratorUserId = initiatorCuratorId,
            Commission = t.Commission.Select(m => new CommissionMemberDto
            {
                Id = m.Id,
                UserId = m.UserId,
                UserName = m.User?.FullName ?? "—",
                Role = m.Role,
                RoleTitle = RoleTitle(m.Role),
                IsBoardMember = m.IsBoardMember,
                IsAccountant = m.IsAccountant,
                IsLegal = m.IsLegal,
                IsSecurity = m.IsSecurity,
                AttendedOpening = m.AttendedOpening,
                DissentingOpinion = m.DissentingOpinion,
                IsVoting = IsVoting(m.Role),
                Conclusion = m.Conclusion,
                ConclusionAttachmentId = m.ConclusionAttachmentId,
                ConclusionAt = m.ConclusionAt,
            }).ToList(),
            Bids = t.Bids.OrderBy(b => b.Price).Select(b => new TenderBidDto
            {
                Id = b.Id,
                SupplierId = b.SupplierId,
                SupplierTitle = b.Supplier!.Title,
                SupplierInn = b.Supplier.Inn,
                Price = b.Price,
                SubmittedOn = b.SubmittedOn,
                IsLate = b.IsLate,
                IsAdmitted = b.IsAdmitted,
                RejectionReason = b.RejectionReason,
                Score = b.Score,
                Specification = b.Specification,
                IsWinner = b.IsWinner,
                SupplierBlacklisted = b.Supplier.IsBlacklisted,
                Votes = b.Votes
                    .OrderBy(v => v.Member!.Role)
                    .Select(v => new BidVoteDto
                    {
                        MemberId = v.MemberId,
                        MemberName = v.Member?.User?.FullName ?? "—",
                        RoleTitle = RoleTitle(v.Member!.Role),
                        IsChairman = v.Member.Role == CommissionRole.Chairman,
                        Choice = v.Choice,
                        ChoiceTitle = ChoiceTitle(v.Choice),
                    }).ToList(),
            }).ToList(),
        };

        foreach (var bid in dto.Bids)
            FillVoteOutcome(bid);

        dto.MeetingChanges = t.MeetingChanges
            .OrderBy(c => c.At)
            .Select(c => new MeetingChangeDto
            {
                FromDate = c.FromDate,
                ToDate = c.ToDate,
                Reason = c.Reason,
                ByUserName = c.By?.FullName ?? "—",
                At = c.At,
            }).ToList();

        // Имя файла заключения — чтобы в карточке была видна не цифра вложения,
        // а название документа, который эксперт приложил.
        var conclusionFiles = dto.Commission
            .Where(m => m.ConclusionAttachmentId is not null)
            .Select(m => m.ConclusionAttachmentId!.Value)
            .ToList();

        if (conclusionFiles.Count > 0)
        {
            var names = await _db.DocumentAttachments
                .Where(a => conclusionFiles.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.FileName);

            foreach (var m in dto.Commission.Where(m => m.ConclusionAttachmentId is not null))
                m.ConclusionFileName = names.GetValueOrDefault(m.ConclusionAttachmentId!.Value);
        }

        // Секретарь ведёт протокол, эксперт даёт заключение — ни тот, ни другой не
        // голосуют, поэтому в кворум и в требования к составу они не входят.
        var voting = t.Commission.Count(m => IsVoting(m.Role));
        dto.QuorumRequired = (int)Math.Ceiling(voting * 2m / 3m);
        dto.Attended = t.Commission.Count(m => IsVoting(m.Role) && m.AttendedOpening);
        dto.HasQuorum = voting > 0 && dto.Attended >= dto.QuorumRequired;

        FillBlockers(dto, t, voting);
        return dto;
    }

    /// <summary>
    /// Итог голосования по заявке (п. 24.2–24.3 Положения).
    ///
    /// Решение принято, если «за» — большинство голосовавших. Воздержавшиеся
    /// считаются голосовавшими: они пришли и приняли участие, просто не поддержали
    /// ни одну сторону, — поэтому большинство считается от всех поданных голосов.
    /// При равенстве «за» и «против» решает голос председателя.
    /// </summary>
    private static void FillVoteOutcome(TenderBidDto bid)
    {
        bid.VotesFor = bid.Votes.Count(v => v.Choice == VoteChoice.For);
        bid.VotesAgainst = bid.Votes.Count(v => v.Choice == VoteChoice.Against);
        bid.VotesAbstained = bid.Votes.Count(v => v.Choice == VoteChoice.Abstained);

        if (bid.Votes.Count == 0)
        {
            bid.Carried = false;
            bid.VoteOutcomeNote = null;
            return;
        }

        if (bid.VotesFor > bid.Votes.Count / 2)
        {
            bid.Carried = true;
            bid.VoteOutcomeNote = $"За — {bid.VotesFor} из {bid.Votes.Count}";
            return;
        }

        if (bid.VotesFor == bid.VotesAgainst)
        {
            var chairman = bid.Votes.FirstOrDefault(v => v.IsChairman);

            if (chairman is null)
            {
                bid.Carried = false;
                bid.VoteOutcomeNote =
                    $"Голоса разделились поровну ({bid.VotesFor} на {bid.VotesAgainst}), " +
                    "а председатель не голосовал — решающего голоса нет (п. 24.3 Положения)";
                return;
            }

            bid.Carried = chairman.Choice == VoteChoice.For;
            bid.VoteOutcomeNote = bid.Carried
                ? $"Голоса разделились поровну ({bid.VotesFor} на {bid.VotesAgainst}); " +
                  "принято решающим голосом председателя (п. 24.3 Положения)"
                : $"Голоса разделились поровну ({bid.VotesFor} на {bid.VotesAgainst}); " +
                  "председатель голосовал против — его голос решающий (п. 24.3 Положения)";
            return;
        }

        bid.Carried = false;
        bid.VoteOutcomeNote =
            $"За — {bid.VotesFor} из {bid.Votes.Count}: большинства нет";
    }

    private static string ChoiceTitle(VoteChoice choice) => choice switch
    {
        VoteChoice.For => "За",
        VoteChoice.Against => "Против",
        VoteChoice.Abstained => "Воздержался",
        _ => choice.ToString(),
    };

    private static void FillBlockers(TenderDto dto, Tender t, int voting)
    {
        // Положение задаёт не «не менее», а ровно столько: пять сотрудников Банка
        // (п. 120). Требование про нечётный состав было нашей выдумкой — в
        // Положении его нет, а равенство голосов оно всё равно не исключает:
        // от равенства защищает решающий голос председателя (п. 24.3).
        if (voting != dto.RequiredSize)
            dto.Blockers.Add(
                $"Состав комиссии — {dto.RequiredSize} членов с правом голоса, сейчас {voting} (п. 120 Положения)");

        var chairman = t.Commission.FirstOrDefault(m => m.Role == CommissionRole.Chairman);

        if (chairman is null)
            dto.Blockers.Add("Не назначен председатель комиссии");
        else
        {
            if (!chairman.IsBoardMember)
                dto.Blockers.Add("Председателем комиссии назначается член Правления (п. 120 Положения)");

            // Куратор инициатора в председателях — прямой конфликт интересов:
            // он же и заинтересован в закупке, которую комиссия оценивает.
            if (dto.InitiatorCuratorUserId is { } curatorId && chairman.UserId == curatorId)
                dto.Blockers.Add(
                    "Председателем не может быть куратор инициатора закупки (п. 120 Положения)");
        }

        var boardMembers = t.Commission.Count(m => m.IsBoardMember && IsVoting(m.Role));
        if (dto.RequiredBoardMembers > 0 && boardMembers < dto.RequiredBoardMembers)
            dto.Blockers.Add(
                $"В комиссии должно быть не менее {dto.RequiredBoardMembers} членов Правления, сейчас {boardMembers}");

        // Постоянные члены по п. 120 — без оговорок про сумму закупки.
        if (!t.Commission.Any(m => m.IsAccountant))
            dto.Blockers.Add("В составе комиссии нет представителя УБУиО (п. 120 Положения)");

        if (!t.Commission.Any(m => m.IsLegal))
            dto.Blockers.Add("В составе комиссии нет представителя Юридической службы (п. 120 Положения)");

        if (!t.Commission.Any(m => m.IsSecurity))
            dto.Blockers.Add("В составе комиссии нет представителя Управления безопасности (п. 120 Положения)");

        if (string.IsNullOrWhiteSpace(t.CommissionOrderNumber))
            dto.Blockers.Add("Не указан приказ Председателя Правления об утверждении комиссии");

        if (t.Status == TenderStatus.Opened && !dto.HasQuorum)
            dto.Blockers.Add($"Нет кворума: {dto.Attended} из {dto.QuorumRequired}");
    }

    private static string StatusTitle(TenderStatus status) => status switch
    {
        TenderStatus.Published => "Объявлен",
        TenderStatus.Opened => "Заявки вскрыты",
        TenderStatus.Decided => "Победитель определён",
        TenderStatus.Failed => "Не состоялся",
        TenderStatus.Cancelled => "Отменён",
        _ => "Подготовка",
    };

    private static string RoleTitle(CommissionRole role) => role switch
    {
        CommissionRole.Chairman => "Председатель",
        CommissionRole.Secretary => "Секретарь",
        CommissionRole.Expert => "Эксперт без права голоса",
        _ => "Член комиссии",
    };
}
