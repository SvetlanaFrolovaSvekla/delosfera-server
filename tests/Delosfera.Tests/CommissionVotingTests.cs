using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Procurement.Services;

namespace Delosfera.Tests;

/// <summary>
/// Голосование комиссии по закупкам — пункты 24.2 и 24.3 Положения.
///
/// «Решения Комиссии по закупке принимаются очно, путем открытого голосования и
/// признается принятым, если за проголосовали большинство членов комиссии.»
/// «В случае равного количества "За" или "Против" голос Председателя комиссии
/// считается преобладающим/решающим.»
///
/// До этой правки победитель назначался одним нажатием, без голосов вовсе. Ошибка
/// здесь дороже обычной: протокол закупки с неверно посчитанным решением
/// оспаривается целиком, а вместе с ним и договор.
///
/// Случай с решающим голосом проверяется в обе стороны. Реализация, которая просто
/// объявляет заявку принятой при равенстве, пройдёт проверку «председатель за» и
/// провалит «председатель против» — поэтому обе нужны.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CommissionVotingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Большинство_за_решение_принято()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.For),
            (члены.Члены[0], VoteChoice.For),
            (члены.Члены[1], VoteChoice.For),
            (члены.Члены[2], VoteChoice.Against),
            (члены.Члены[3], VoteChoice.Abstained)), актор);

        var bid = card.Bids.Single(b => b.Id == bidId);

        Assert.True(bid.Carried);
        Assert.Equal(3, bid.VotesFor);
        Assert.Equal(1, bid.VotesAgainst);
        Assert.Equal(1, bid.VotesAbstained);
    }

    [Fact]
    public async Task Меньшинство_за_решение_не_принято()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.Against),
            (члены.Члены[0], VoteChoice.For),
            (члены.Члены[1], VoteChoice.For),
            (члены.Члены[2], VoteChoice.Against),
            (члены.Члены[3], VoteChoice.Against)), актор);

        Assert.False(card.Bids.Single(b => b.Id == bidId).Carried);
    }

    [Fact]
    public async Task Воздержавшиеся_считаются_в_общем_числе_голосовавших()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        // Двое за, один против, двое воздержались: «за» — два голоса из пяти,
        // большинства нет. Если воздержавшихся выкинуть из знаменателя, получится
        // два из трёх, и заявка пройдёт — именно так и теряется большинство.
        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.For),
            (члены.Члены[0], VoteChoice.For),
            (члены.Члены[1], VoteChoice.Against),
            (члены.Члены[2], VoteChoice.Abstained),
            (члены.Члены[3], VoteChoice.Abstained)), актор);

        Assert.False(card.Bids.Single(b => b.Id == bidId).Carried);
    }

    [Fact]
    public async Task При_равенстве_голос_председателя_за_решает_в_пользу()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.For),
            (члены.Члены[0], VoteChoice.For),
            (члены.Члены[1], VoteChoice.Against),
            (члены.Члены[2], VoteChoice.Against)), актор);

        var bid = card.Bids.Single(b => b.Id == bidId);

        Assert.True(bid.Carried);
        Assert.Contains("решающим голосом председателя", bid.VoteOutcomeNote);
    }

    [Fact]
    public async Task При_равенстве_голос_председателя_против_решает_отказом()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.Against),
            (члены.Члены[0], VoteChoice.Against),
            (члены.Члены[1], VoteChoice.For),
            (члены.Члены[2], VoteChoice.For)), актор);

        var bid = card.Bids.Single(b => b.Id == bidId);

        Assert.False(bid.Carried);
        Assert.Contains("председатель голосовал против", bid.VoteOutcomeNote);
    }

    [Fact]
    public async Task Победителем_нельзя_объявить_заявку_без_голосования()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, _) = await ЗавестиКонкурсАsync(db);

        var tenderId = db.TenderBids.Single(b => b.Id == bidId).TenderId;

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeclareWinnerAsync(tenderId, bidId, актор));

        Assert.Contains("голосованием", ошибка.Message);
    }

    [Fact]
    public async Task Голос_не_принимается_от_отсутствовавшего_на_заседании()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        // Явка снята: человек на заседании не был, значит и голосовать не мог.
        var отсутствующий = db.CommissionMembers.Single(m => m.Id == члены.Члены[0]);
        отсутствующий.AttendedOpening = false;
        await db.SaveChangesAsync();

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordVotesAsync(bidId, Голоса(
                (члены.Председатель, VoteChoice.For),
                (члены.Члены[0], VoteChoice.For)), актор));

        Assert.Contains("не отмечен как участник заседания", ошибка.Message);
    }

    [Fact]
    public async Task Повторное_внесение_заменяет_голоса_а_не_добавляет()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, члены) = await ЗавестиКонкурсАsync(db);

        await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.For),
            (члены.Члены[0], VoteChoice.For)), актор);

        var card = await service.RecordVotesAsync(bidId, Голоса(
            (члены.Председатель, VoteChoice.Against),
            (члены.Члены[0], VoteChoice.Against)), актор);

        var bid = card.Bids.Single(b => b.Id == bidId);

        Assert.Equal(2, bid.Votes.Count);
        Assert.Equal(0, bid.VotesFor);
        Assert.Equal(2, bid.VotesAgainst);
    }

    [Fact]
    public async Task Перенос_заседания_требует_основания_и_остаётся_записью()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var (service, bidId, _) = await ЗавестиКонкурсАsync(db);
        var tenderId = db.TenderBids.Single(b => b.Id == bidId).TenderId;

        var новая = new DateOnly(2026, 9, 15);

        var ошибка = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ScheduleMeetingAsync(tenderId, new MeetingScheduleRequest {Date = новая}, актор));
        Assert.Contains("основание переноса", ошибка.Message);

        var card = await service.ScheduleMeetingAsync(
            tenderId,
            new MeetingScheduleRequest {Date = новая, Reason = "Не собрался кворум"},
            актор);

        Assert.Equal(новая, card.MeetingDate);

        // Первая запись — назначение заседания, вторая — перенос с основанием.
        Assert.Equal(2, card.MeetingChanges.Count);
        Assert.Equal("Не собрался кворум", card.MeetingChanges[^1].Reason);
    }

    // ── подготовка ───────────────────────────────────────────────────────────

    private const int актор = 1;

    /// <summary>Со второго десятка: единица занята актором.</summary>
    private static int _nextUserId = 10;

    private static BidVotesRequest Голоса(params (int MemberId, VoteChoice Choice)[] голоса) =>
        new()
        {
            Votes = голоса
                .Select(г => new BidVotesRequest.MemberVote {MemberId = г.MemberId, Choice = г.Choice})
                .ToList(),
        };

    private sealed record Состав(int Председатель, int[] Члены);

    /// <summary>
    /// Конкурс с вскрытой заявкой и комиссией из пяти голосующих, все присутствуют.
    /// Пять — состав по п. 120 Положения, поэтому и в тестах он такой же: проверять
    /// голосование на составе, который Положение не допускает, смысла нет.
    /// </summary>
    private static async Task<(TenderService Service, int BidId, Состав Члены)> ЗавестиКонкурсАsync(
        DelosferaDbContext db)
    {
        var service = new TenderService(db, new NoopAudit(), new BankClock());

        var method = await db.ProcurementMethods
            .FirstAsync(m => m.Code == ProcurementMethodCode.TenderOpen);

        // Заявка живёт поверх общей карточки документа — внешний ключ на настоящей
        // базе проверяется, поэтому документ заводится по-настоящему.
        TestSupport.EnsureUser(db, актор);

        var document = new delosfera_server.Modules.Documents.Models.Document
        {
            Type = delosfera_server.Modules.Documents.Models.DocumentType.Procurement,
            Title = $"Тестовая закупка {Guid.NewGuid():N}"[..40],
            StatusCode = ProcurementStatus.InProcurement,
            AuthorId = актор,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = document.Id,
            Subject = document.Title,
            Amount = 1_000_000m,
            MethodId = method.Id,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var tender = new Tender
        {
            RequestId = request.Id,
            Status = TenderStatus.Opened,
            MeetingDate = new DateOnly(2026, 9, 1),
        };
        db.Tenders.Add(tender);
        await db.SaveChangesAsync();

        db.TenderMeetingChanges.Add(new TenderMeetingChange
        {
            TenderId = tender.Id,
            ToDate = tender.MeetingDate!.Value,
            Reason = "Заседание назначено",
            ByUserId = актор,
            At = DateTime.UtcNow,
        });

        var supplier = new Supplier {Title = $"Поставщик {Guid.NewGuid():N}"[..30]};
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var bid = new TenderBid
        {
            TenderId = tender.Id,
            SupplierId = supplier.Id,
            Price = 900_000m,
            SubmittedOn = new DateOnly(2026, 8, 25),
            IsAdmitted = true,
        };
        db.TenderBids.Add(bid);

        var председатель = НовыйЧлен(db, tender.Id, CommissionRole.Chairman);
        var члены = Enumerable.Range(0, 4)
            .Select(_ => НовыйЧлен(db, tender.Id, CommissionRole.Member))
            .ToArray();

        await db.SaveChangesAsync();

        return (service, bid.Id, new Состав(председатель.Id, члены.Select(m => m.Id).ToArray()));
    }

    private static CommissionMember НовыйЧлен(DelosferaDbContext db, int tenderId, CommissionRole role)
    {
        // База у каждого теста своя, но внутри одного конкурса сотрудник входит в
        // комиссию ровно один раз — на это стоит уникальный индекс.
        var userId = Interlocked.Increment(ref _nextUserId);
        TestSupport.EnsureUser(db, userId);

        var member = new CommissionMember
        {
            TenderId = tenderId,
            UserId = userId,
            Role = role,
            IsBoardMember = role == CommissionRole.Chairman,
            AttendedOpening = true,
        };

        db.CommissionMembers.Add(member);
        return member;
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string entityType, int entityId, string action, int? userId, object? payload = null) =>
            Task.CompletedTask;
    }
}
